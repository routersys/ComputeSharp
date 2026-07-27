using System;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Win32;

#pragma warning disable CA1063

namespace ComputeSharp;

/// <summary>
/// A registration pairing the compute queue of a graphics device with the external queue of one interop provider.
/// </summary>
public sealed unsafe class ComputeInteropDomain : IDisposable
{
    /// <summary>
    /// The device the current domain is registered on.
    /// </summary>
    private readonly GraphicsDevice device;

    /// <summary>
    /// The registry of the device the current domain is registered in.
    /// </summary>
    private readonly DeviceRegistrationRegistry registry;

    /// <summary>
    /// The identifier of the current domain.
    /// </summary>
    private readonly ExternalDomainId id;

    /// <summary>
    /// The capabilities the provider of the current domain declared when it was registered.
    /// </summary>
    private readonly ExternalInteropCapabilities capabilities;

    /// <summary>
    /// The endpoint of the provider the current domain owns.
    /// </summary>
    private readonly ExternalProviderEndpoint endpoint;

    /// <summary>
    /// The registration reference the current domain holds over the scheduler of its provider.
    /// </summary>
    private readonly SchedulerRegistration schedulerRegistration;

    /// <summary>
    /// The gate protecting <see cref="record"/>.
    /// </summary>
    private readonly Lock gate = new();

    /// <summary>
    /// The shared fence backing the timeline of the current domain.
    /// </summary>
    private ComPtr<ID3D12Fence> d3D12SharedFence;

    /// <summary>
    /// The state and the outstanding references of the current domain.
    /// </summary>
    private ComputeInteropDomainRecord record;

    /// <summary>
    /// The single external operation the current domain admits at a time.
    /// </summary>
    private DomainOperationRecord operation;

    /// <summary>
    /// The first exception a provider call of the current domain failed with, if any.
    /// </summary>
    private Exception? providerDiagnostic;

    /// <summary>
    /// The reason the current domain was poisoned, if it was.
    /// </summary>
    private Exception? poisonReason;

    /// <summary>
    /// Creates a new <see cref="ComputeInteropDomain"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The device the domain is registered on.</param>
    /// <param name="registry">The registry of <paramref name="device"/>.</param>
    /// <param name="id">The identifier of the domain.</param>
    /// <param name="capabilities">The capabilities the provider declared.</param>
    /// <param name="endpoint">The endpoint of the provider the domain takes ownership of.</param>
    /// <param name="schedulerRegistration">The registration reference over the scheduler of the provider.</param>
    /// <param name="d3D12SharedFence">The shared fence backing the timeline of the domain.</param>
    internal ComputeInteropDomain(
        GraphicsDevice device,
        DeviceRegistrationRegistry registry,
        ExternalDomainId id,
        ExternalInteropCapabilities capabilities,
        ExternalProviderEndpoint endpoint,
        SchedulerRegistration schedulerRegistration,
        ComPtr<ID3D12Fence> d3D12SharedFence)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(registry);
        default(ArgumentNullException).ThrowIfNull(endpoint);
        default(ArgumentNullException).ThrowIfNull(schedulerRegistration);
        default(ArgumentException).ThrowIf(id.Value == 0, nameof(id));
        default(ArgumentException).ThrowIf(d3D12SharedFence.Get() is null, nameof(d3D12SharedFence));

        this.device = device;
        this.registry = registry;
        this.id = id;
        this.capabilities = capabilities;
        this.endpoint = endpoint;
        this.schedulerRegistration = schedulerRegistration;
        this.d3D12SharedFence = d3D12SharedFence;
        this.record = new ComputeInteropDomainRecord();
        this.operation = new DomainOperationRecord(id);
    }

    /// <summary>
    /// Gets the device the current domain is registered on.
    /// </summary>
    public GraphicsDevice Device => this.device;

    /// <summary>
    /// Gets the identifier of the current domain.
    /// </summary>
    public ExternalDomainId Id => this.id;

    /// <summary>
    /// Gets the capabilities the provider of the current domain declared when it was registered.
    /// </summary>
    public ExternalInteropCapabilities Capabilities => this.capabilities;

    /// <summary>
    /// Gets whether the disposal of the current domain has been requested.
    /// </summary>
    public bool IsDisposeRequested
    {
        get
        {
            lock (this.gate)
            {
                return this.record.IsDisposeRequested;
            }
        }
    }

    /// <summary>
    /// Gets whether the current domain completed its disposal.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (this.gate)
            {
                return this.record.IsDisposed;
            }
        }
    }

    /// <summary>
    /// Gets the first exception a provider call of the current domain failed with, if any.
    /// </summary>
    internal Exception? ProviderDiagnostic => Volatile.Read(ref this.providerDiagnostic);

    /// <summary>
    /// Requests the disposal of the current domain.
    /// </summary>
    /// <remarks>
    /// This releases the owner reference of the domain and rejects new work. The native objects of the
    /// domain are released once every other reference over it has been released too.
    /// </remarks>
    public void Dispose()
    {
        lock (this.gate)
        {
            _ = this.record.TryRequestDispose();
            _ = this.record.TryReleaseOwner();
        }

        TryReleaseNative();
    }

    /// <summary>
    /// Waits for the disposal of the current domain to complete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the disposal of the domain has not been requested.</exception>
    public void WaitForDisposal()
    {
        while (!IsDisposed)
        {
            default(InvalidOperationException).ThrowIf(
                !IsDisposeRequested,
                "The interop domain has not been disposed, so its disposal cannot complete.");

            ulong progress = this.registry.Coordinator.ProgressVersion;

            TryReleaseNative();

            if (IsDisposed)
            {
                this.registry.Coordinator.Wake();

                return;
            }

            default(InvalidOperationException).ThrowIf(
                !this.registry.Coordinator.TryWaitForProgress(progress),
                "The completion coordinator of the device stopped before the interop domain was released.");
        }
    }

    /// <summary>
    /// Gets the reason the current domain was poisoned, if it was.
    /// </summary>
    internal Exception? PoisonReason => Volatile.Read(ref this.poisonReason);

    /// <summary>
    /// Poisons the current domain, so that it starts its teardown without waiting for its references.
    /// </summary>
    /// <param name="reason">The reason the current domain can no longer be trusted.</param>
    internal void MarkPoisoned(Exception reason)
    {
        default(ArgumentNullException).ThrowIfNull(reason);

        _ = Interlocked.CompareExchange(ref this.poisonReason, reason, null);

        bool isPoisoned;

        lock (this.gate)
        {
            isPoisoned = this.record.TryMarkPoisoned() && this.record.TryBeginTeardown();
        }

        if (isPoisoned)
        {
            TryReleaseNative();
        }
    }

    /// <summary>
    /// Acquires the single external operation of the current domain, with its scheduler reservation.
    /// </summary>
    /// <param name="reference">The kind of domain reference the operation holds.</param>
    /// <param name="boundGeneration">The resource generation the operation is bound to.</param>
    /// <param name="releaseExternalReferenceOnDispose">Whether releasing the operation also releases an external reference.</param>
    /// <param name="lease">The acquired operation lease, if the acquisition succeeded.</param>
    /// <param name="schedulerFailure">The exception the scheduler reservation failed with, if it did.</param>
    /// <returns>The outcome of the acquisition.</returns>
    /// <remarks>
    /// The acquisition order is domain reference, domain permit, then scheduler reservation. Every failure
    /// releases what it already took, in reverse order, and leaves no native side effect behind.
    /// </remarks>
    internal DomainOperationStatus TryAcquireOperation(
        ExternalDomainReference reference,
        ResourceGenerationId boundGeneration,
        bool releaseExternalReferenceOnDispose,
        out DomainOperationLease lease,
        out Exception? schedulerFailure)
    {
        lease = default;
        schedulerFailure = null;

        DomainOperationStatus status;
        ulong token = 0;

        lock (this.gate)
        {
            if (!this.record.TryAcquire(reference))
            {
                status = DomainOperationStatus.DomainUnavailable;
            }
            else
            {
                status = this.operation.TryAcquire(boundGeneration, releaseExternalReferenceOnDispose, out token);

                if (status is not DomainOperationStatus.Acquired)
                {
                    _ = this.record.TryRelease(reference);
                }
            }
        }

        if (status is DomainOperationStatus.TokenExhausted)
        {
            MarkPoisoned(new InvalidOperationException(
                $"""The operation token sequence of the interop domain "{this.id.Value}" is exhausted."""));
        }

        if (status is not DomainOperationStatus.Acquired)
        {
            return status;
        }

        try
        {
            this.schedulerRegistration.EnterReservation();
        }
        catch (Exception e)
        {
            schedulerFailure = e;

            lock (this.gate)
            {
                _ = this.operation.TryRelease(token);
                _ = this.record.TryRelease(reference);
            }

            return DomainOperationStatus.SchedulerBusy;
        }

        lease = new DomainOperationLease(this, reference, token);

        return DomainOperationStatus.Acquired;
    }

    /// <summary>
    /// Releases the external operation a lease of the current domain holds.
    /// </summary>
    /// <param name="reference">The kind of domain reference the operation holds.</param>
    /// <param name="token">The token the operation was acquired with.</param>
    /// <remarks>
    /// Only the token the operation was acquired with releases it, so a copy of an already released lease
    /// does nothing. The release order is the reverse of the acquisition order.
    /// </remarks>
    internal void ReleaseOperation(ExternalDomainReference reference, ulong token)
    {
        bool isReleasing;

        lock (this.gate)
        {
            isReleasing = this.operation.TryBeginRelease(token);
        }

        if (!isReleasing)
        {
            return;
        }

        try
        {
            this.schedulerRegistration.ExitReservation();
        }
        finally
        {
            lock (this.gate)
            {
                this.operation.CompleteRelease();

                _ = this.record.TryRelease(reference);
            }

            TryReleaseNative();

            this.registry.Coordinator.Wake();
        }
    }

    /// <summary>
    /// Moves the current domain to its terminal state and notifies its provider.
    /// </summary>
    /// <param name="reason">The reason the device of the current domain is terminal.</param>
    internal void MarkDeviceTerminal(Exception reason)
    {
        bool isTerminal;

        lock (this.gate)
        {
            isTerminal = this.record.TryMarkTerminal();
        }

        if (!isTerminal)
        {
            return;
        }

        if (this.endpoint.NotifyDeviceTerminal(reason) is Exception diagnostic)
        {
            SaveProviderDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Releases the current domain on behalf of the teardown of its device.
    /// </summary>
    /// <remarks>
    /// A provider failing here is saved as a diagnostic rather than propagated, so that one faulty provider
    /// cannot stop the teardown from reaching the remaining domains and the native objects of the device.
    /// </remarks>
    internal void ReleaseForDeviceTeardown()
    {
        try
        {
            Dispose();
        }
        catch (Exception e)
        {
            SaveProviderDiagnostic(e);
        }

        bool isReleasingNative;

        lock (this.gate)
        {
            isReleasingNative = this.record.TryBeginReleasingNativeForDeviceTeardown();
        }

        if (!isReleasingNative)
        {
            return;
        }

        try
        {
            ReleaseNative();
        }
        catch (Exception e)
        {
            SaveProviderDiagnostic(e);
        }
    }

    /// <summary>
    /// Saves the first exception a provider call of the current domain failed with.
    /// </summary>
    /// <param name="diagnostic">The exception to save.</param>
    private void SaveProviderDiagnostic(Exception diagnostic)
    {
        _ = Interlocked.CompareExchange(ref this.providerDiagnostic, diagnostic, null);
    }

    /// <summary>
    /// Releases the native objects of the current domain, if it holds no reference anymore.
    /// </summary>
    private void TryReleaseNative()
    {
        bool isReleasingNative;

        lock (this.gate)
        {
            isReleasingNative = this.record.TryBeginReleasingNative();
        }

        if (isReleasingNative)
        {
            ReleaseNative();
        }
    }

    /// <summary>
    /// Releases the provider, the scheduler registration and the shared fence of the current domain.
    /// </summary>
    private void ReleaseNative()
    {
        try
        {
            this.endpoint.DisposeProvider();
        }
        finally
        {
            this.schedulerRegistration.Release();
            this.d3D12SharedFence.Dispose();

            lock (this.gate)
            {
                _ = this.record.TryCompleteDisposal();
            }

            this.registry.UnregisterDomain(this);
        }
    }
}
