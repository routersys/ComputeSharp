using System;
using System.Threading;
using ComputeSharp.Graphics.Helpers;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Interop;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Win32;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a shared texture declared by a compute interop resource set.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
/// <typeparam name="TPixel">The type of pixels used on the GPU side.</typeparam>
/// <typeparam name="TView">The type of the external view of the texture.</typeparam>
public sealed unsafe class SharedTextureSlot<T, TPixel, TView> : IComputeSharedResourceSlot, IDisposable, IComputeSharedSlot
    where T : unmanaged, IPixel<T, TPixel>
    where TPixel : unmanaged
    where TView : class, IDisposable
{
    /// <summary>
    /// The gate protecting the state of the current slot.
    /// </summary>
    private SlotGate slotGate;

    /// <summary>
    /// The exclusion protecting <see cref="maintenance"/>.
    /// </summary>
    private SpinLock maintenanceExclusion;

    /// <summary>
    /// The single maintenance record the current slot preallocates for its external drains.
    /// </summary>
    private ExternalMaintenanceRecord maintenance;

    /// <summary>
    /// The resource set the current slot is bound to, or <see langword="null"/> if it is not bound.
    /// </summary>
    private InteropResourceSetRuntime? runtime;

    /// <summary>
    /// The ordinal of the current slot within the resource set it is bound to.
    /// </summary>
    private SlotOrdinal ordinal;

    /// <summary>
    /// Creates a new <see cref="SharedTextureSlot{T, TPixel, TView}"/> instance that is not bound to a resource set.
    /// </summary>
    public SharedTextureSlot()
    {
    }

    /// <summary>
    /// Gets the current logical width of the shared texture.
    /// </summary>
    public int Width
    {
        get
        {
            this.slotGate.GetActiveLogicalExtent(out int width, out _);

            return width;
        }
    }

    /// <summary>
    /// Gets the current logical height of the shared texture.
    /// </summary>
    public int Height
    {
        get
        {
            this.slotGate.GetActiveLogicalExtent(out _, out int height);

            return height;
        }
    }

    /// <summary>
    /// Gets whether the current slot owns a published texture generation.
    /// </summary>
    public bool IsAllocated => this.slotGate.IsAllocated;

    /// <summary>
    /// Gets whether disposal of the current slot has been requested.
    /// </summary>
    internal bool IsDisposeRequested => this.slotGate.IsDisposeRequested;

    /// <inheritdoc/>
    bool IComputeSharedSlot.IsDisposalComplete => this.slotGate.IsDisposalComplete;

    /// <inheritdoc/>
    bool IComputeSharedSlot.TryBind(
        InteropResourceSetRuntime runtime,
        SlotOrdinal ordinal,
        int[] planStorage,
        in SlotResourcePlanStateRecord planState)
    {
        default(InvalidOperationException).ThrowIf(
            DXGIFormatHelper.GetForType<T>() != DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            "A shared texture slot only stores the pixel type the shared texture native descriptor is fixed to.");
        default(InvalidOperationException).ThrowIf(
            runtime.Domain.TryGetEndpoint<TView>() is null,
            "The interop domain does not create external views of the type the shared texture slot declares.");

        this.runtime = runtime;
        this.ordinal = ordinal;
        this.maintenance = new ExternalMaintenanceRecord(runtime.Domain.Id, runtime.Id, ordinal);

        if (this.slotGate.TryBind(planStorage, in planState))
        {
            return true;
        }

        this.runtime = null;

        return false;
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.RequestDispose()
    {
        Dispose();
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.RunMaintenance()
    {
        RunMaintenance();
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.MarkTerminalRetained()
    {
        _ = this.slotGate.TryMarkDeviceTerminal();
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.ReleaseTerminalGenerations()
    {
        SlotTerminalRelease.Run(ref this.slotGate);
    }

    /// <summary>
    /// Ensures the shared texture matches the requested logical dimensions.
    /// </summary>
    /// <param name="width">The requested logical width.</param>
    /// <param name="height">The requested logical height.</param>
    /// <param name="changed">Whether the published texture generation was replaced.</param>
    /// <returns>Whether the shared texture matches the requested logical dimensions.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the slot is not bound, or if its resource set no longer accepts work.</exception>
    /// <exception cref="GraphicsMemoryAllocationException">Thrown if the native allocation fails for a reason other than memory pressure.</exception>
    public bool TryEnsure(int width, int height, out bool changed)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(width);
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(height);

        changed = false;

        ThrowIfNotBound();

        InteropResourceSetRuntime runtime = this.runtime!;
        GraphicsDevice device = runtime.Device;

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.ThrowIfDeviceTerminal();

        default(InvalidOperationException).ThrowIf(
            runtime.State is not RegistrationState.Active,
            "The compute interop resource set no longer accepts work.");

        ref readonly SharedTextureContractDescriptor descriptor = ref runtime.Descriptor.SharedTextures.Span[(int)this.ordinal.Value];

        RunMaintenance();

        ResourcePlanDecision decision = this.slotGate.EvaluateSharedTexture(in descriptor, width, height);

        if (decision is ResourcePlanDecision.Identical)
        {
            return true;
        }

        ReadOnlySpan<int> requestedPlan = [width, height];

        if (decision is ResourcePlanDecision.LogicalUpdate)
        {
            this.slotGate.GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch);

            return this.slotGate.TryApplyLogicalUpdate(activeSetId, bindingEpoch, requestedPlan);
        }

        return TryReplaceGeneration(runtime, device, in descriptor, requestedPlan, width, height, out changed);
    }

    /// <summary>
    /// Gets a binding to the currently published texture generation.
    /// </summary>
    /// <returns>A binding to the currently published texture generation.</returns>
    public ComputeResourceBinding<ReadWriteTexture2D<T, TPixel>> GetComputeBinding()
    {
        ThrowIfNotBound();

        return this.slotGate.TryGetBinding(0, out ComputeResourceBinding<ReadWriteTexture2D<T, TPixel>> binding)
            ? binding
            : default;
    }

    /// <summary>
    /// Begins a transient external operation over the currently published texture generation.
    /// </summary>
    /// <returns>A transient borrow of the external view.</returns>
    public BorrowedExternalTextureView<TView> BeginExternalOperation()
    {
        ThrowIfNotBound();

        throw new InvalidOperationException("The shared texture slot does not borrow external views yet.");
    }

    /// <summary>
    /// Acquires a persistent lease over the external view of the currently published texture generation.
    /// </summary>
    /// <returns>A persistent lease over the external view.</returns>
    public ExternalTextureLease<TView> AcquireExternalViewLease()
    {
        ThrowIfNotBound();

        throw new InvalidOperationException("The shared texture slot does not lease external views yet.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        PreparedGenerationRollback.RollbackUnpublished(this.slotGate.RequestDispose());

        RunMaintenance();
    }

    /// <summary>
    /// Waits for the disposal of the current slot to complete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if disposal of the current slot has not been requested.</exception>
    /// <remarks>
    /// A slot that is not bound to a resource set has nothing to wait for and returns immediately.
    /// </remarks>
    public void WaitForDisposal()
    {
        if (this.runtime is not InteropResourceSetRuntime boundRuntime)
        {
            return;
        }

        while (!this.slotGate.IsDisposalComplete)
        {
            default(InvalidOperationException).ThrowIf(
                !this.slotGate.IsDisposeRequested,
                "The shared texture slot has not been disposed.");

            ulong progress = boundRuntime.Registry.Coordinator.ProgressVersion;

            RunMaintenance();

            if (this.slotGate.IsDisposalComplete)
            {
                boundRuntime.Registry.Coordinator.Wake();

                return;
            }

            default(InvalidOperationException).ThrowIf(
                !boundRuntime.Registry.Coordinator.TryWaitForProgress(progress),
                "The completion coordinator of the device stopped before the shared texture slot was released.");
        }
    }

    /// <summary>
    /// Runs the external drain and the generation maintenance of the current slot.
    /// </summary>
    private void RunMaintenance()
    {
        if (this.runtime is InteropResourceSetRuntime boundRuntime)
        {
            while (TryRunExternalMaintenancePass(boundRuntime))
            {
            }
        }

        SlotGenerationMaintenance.Run(ref this.slotGate);
    }

    /// <summary>
    /// Runs a single external drain pass over the generations of the current slot.
    /// </summary>
    /// <param name="runtime">The resource set the current slot is bound to.</param>
    /// <returns>Whether the pass released the external objects of a generation.</returns>
    private bool TryRunExternalMaintenancePass(InteropResourceSetRuntime runtime)
    {
        this.slotGate.GetMaintenanceHandles(
            out ResourceGenerationSetHandle active,
            out ResourceGenerationSetHandle prepared,
            out ResourceGenerationSetHandle retired,
            out bool isRetiringActive);

        ResourceGenerationOwner? owner = TryGetPendingExternalRelease(in retired);

        if (owner is null && isRetiringActive)
        {
            owner = TryGetPendingExternalRelease(in active) ?? TryGetPendingExternalRelease(in prepared);
        }

        return owner is not null && TryReleaseExternalObjects(runtime, owner);
    }

    /// <summary>
    /// Gets the generation of a handle whose external objects are still held, if there is one.
    /// </summary>
    /// <param name="handle">The handle of the generation to inspect.</param>
    /// <returns>The generation still holding external objects, or <see langword="null"/>.</returns>
    private static ResourceGenerationOwner? TryGetPendingExternalRelease(in ResourceGenerationSetHandle handle)
    {
        if (handle.IsEmpty || handle.Owner is not ResourceGenerationOwner owner)
        {
            return null;
        }

        return Volatile.Read(ref owner.GetResourceRecord(0).ExternalObjectsReleased) == 0 ? owner : null;
    }

    /// <summary>
    /// Releases the external objects of a retired generation of the current slot.
    /// </summary>
    /// <param name="runtime">The resource set the current slot is bound to.</param>
    /// <param name="owner">The generation to release the external objects of.</param>
    /// <returns>Whether the external objects were released.</returns>
    /// <remarks>
    /// The external view is released inside a scheduler reservation, as the immediate context it belongs to is
    /// not free threaded. A busy domain or scheduler leaves the record waiting rather than spinning on it.
    /// </remarks>
    private bool TryReleaseExternalObjects(InteropResourceSetRuntime runtime, ResourceGenerationOwner owner)
    {
        ResourceGenerationId generation = owner.GetResourceRecord(0).Id;

        if (!TryRequestExternalRelease(generation))
        {
            return false;
        }

        DomainOperationStatus status = runtime.Domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            generation,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease lease,
            out _);

        if (status is not DomainOperationStatus.Acquired)
        {
            MarkExternalReleaseWaiting(status);

            return false;
        }

        try
        {
            using (lease)
            {
                _ = owner.TryReleaseExternalObjects();
            }
        }
        catch
        {
            MarkExternalReleaseFaulted();

            throw;
        }

        CompleteExternalRelease();

        return true;
    }

    /// <summary>
    /// Moves the maintenance record of the current slot to the external release of a generation.
    /// </summary>
    /// <param name="generation">The generation to release the external objects of.</param>
    /// <returns>Whether the record reached the external release.</returns>
    private bool TryRequestExternalRelease(ResourceGenerationId generation)
    {
        bool taken = false;

        try
        {
            this.maintenanceExclusion.Enter(ref taken);

            if (this.maintenance.IsCompleted)
            {
                _ = this.maintenance.TryReset();
            }

            if (this.maintenance.IsIdle)
            {
                return this.maintenance.TryRequest(generation) &&
                    this.maintenance.TryQueue() &&
                    this.maintenance.TrySkipFinalDrain();
            }

            return this.maintenance.Generation.Value == generation.Value && !this.maintenance.IsFaulted;
        }
        finally
        {
            if (taken)
            {
                this.maintenanceExclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    /// <summary>
    /// Leaves the maintenance record of the current slot waiting for a busy domain or scheduler.
    /// </summary>
    /// <param name="status">The outcome the domain operation was refused with.</param>
    private void MarkExternalReleaseWaiting(DomainOperationStatus status)
    {
        bool taken = false;

        try
        {
            this.maintenanceExclusion.Enter(ref taken);

            if (status is DomainOperationStatus.PermitBusy)
            {
                _ = this.maintenance.TryWaitForDomainPermit();
            }
            else if (status is DomainOperationStatus.SchedulerBusy)
            {
                _ = this.maintenance.TryWaitForScheduler();
            }
            else
            {
                _ = this.maintenance.TryFault();
            }
        }
        finally
        {
            if (taken)
            {
                this.maintenanceExclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    /// <summary>
    /// Faults the maintenance record of the current slot.
    /// </summary>
    private void MarkExternalReleaseFaulted()
    {
        bool taken = false;

        try
        {
            this.maintenanceExclusion.Enter(ref taken);

            _ = this.maintenance.TryFault();
        }
        finally
        {
            if (taken)
            {
                this.maintenanceExclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    /// <summary>
    /// Completes the maintenance record of the current slot.
    /// </summary>
    private void CompleteExternalRelease()
    {
        bool taken = false;

        try
        {
            this.maintenanceExclusion.Enter(ref taken);

            _ = this.maintenance.TryComplete();
        }
        finally
        {
            if (taken)
            {
                this.maintenanceExclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    /// <summary>
    /// Replaces the generation of the current slot with one matching the requested dimensions.
    /// </summary>
    /// <param name="runtime">The resource set the current slot is bound to.</param>
    /// <param name="device">The device of <paramref name="runtime"/>.</param>
    /// <param name="descriptor">The contract of the current slot.</param>
    /// <param name="requestedPlan">The requested plan vector.</param>
    /// <param name="width">The requested logical width.</param>
    /// <param name="height">The requested logical height.</param>
    /// <param name="changed">Whether a new generation was published.</param>
    /// <returns>Whether a new generation was published.</returns>
    private bool TryReplaceGeneration(
        InteropResourceSetRuntime runtime,
        GraphicsDevice device,
        in SharedTextureContractDescriptor descriptor,
        ReadOnlySpan<int> requestedPlan,
        int width,
        int height,
        out bool changed)
    {
        changed = false;

        ComputeGenerationDeclarationStatus status = ComputeGenerationDescriber.DescribeInteropSharedTexture(
            device,
            descriptor.ExternalUsage,
            width,
            height,
            out ComputeGenerationDeclaration declaration);

        if (status is not ComputeGenerationDeclarationStatus.Valid)
        {
            throw new InvalidOperationException($"The shared texture generation is not valid ({status}).");
        }

        if (device.TryReserveMemory(declaration.Placement, declaration.SizeInBytes, out MemoryReservationToken token)
            is not MemoryAdmissionStatus.Admitted)
        {
            return false;
        }

        ResourceGenerationOwner owner = new(
            device,
            device.ResourceIdentities,
            descriptor.Recovery,
            in token,
            1,
            runtime.Domain);

        ulong preparedToken = runtime.CreatePreparedToken();

        if (!this.slotGate.TryInstallPrepared(new ResourceGenerationSetHandle(owner), preparedToken, requestedPlan))
        {
            owner.ReleaseUnpublished();

            return false;
        }

        this.slotGate.GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch);

        bool isCommitted;

        try
        {
            if (!TryCreateGeneration(
                runtime,
                device,
                owner,
                in descriptor,
                in declaration,
                width,
                height,
                activeSetId,
                bindingEpoch,
                preparedToken,
                out isCommitted))
            {
                AbortPreparedGeneration(owner, preparedToken);

                return false;
            }
        }
        catch
        {
            AbortPreparedGeneration(owner, preparedToken);

            throw;
        }

        if (!isCommitted)
        {
            return false;
        }

        owner.CommitAccounting();

        RunMaintenance();

        changed = true;

        return true;
    }

    /// <summary>
    /// Creates and commits a shared texture generation of the current slot.
    /// </summary>
    /// <param name="runtime">The resource set the current slot is bound to.</param>
    /// <param name="device">The device of <paramref name="runtime"/>.</param>
    /// <param name="owner">The generation being created.</param>
    /// <param name="descriptor">The contract of the current slot.</param>
    /// <param name="declaration">The allocation descriptor of the generation.</param>
    /// <param name="width">The requested logical width.</param>
    /// <param name="height">The requested logical height.</param>
    /// <param name="activeSetId">The generation set the slot published when the replacement started.</param>
    /// <param name="bindingEpoch">The binding epoch the slot was at when the replacement started.</param>
    /// <param name="preparedToken">The token the prepared generation was installed with.</param>
    /// <param name="isCommitted">Whether the generation was published.</param>
    /// <returns>Whether the generation was created.</returns>
    /// <remarks>
    /// The domain operation lease is held from the provider call until the slot swap, so that a swap that loses
    /// its race releases the external view under the same scheduler reservation that created it.
    /// </remarks>
    private bool TryCreateGeneration(
        InteropResourceSetRuntime runtime,
        GraphicsDevice device,
        ResourceGenerationOwner owner,
        in SharedTextureContractDescriptor descriptor,
        in ComputeGenerationDeclaration declaration,
        int width,
        int height,
        ResourceGenerationSetId activeSetId,
        ulong bindingEpoch,
        ulong preparedToken,
        out bool isCommitted)
    {
        isCommitted = false;

        HRESULT hresult = device.TryCreateCommittedResource(in declaration.Description, out ComPtr<ID3D12Resource> createdResource);

        using ComPtr<ID3D12Resource> d3D12Resource = createdResource;

        if (hresult < 0)
        {
            NativeAllocationOutcome outcome = MemoryAllocationCoordinator.ClassifyNativeResult(hresult);

            if (outcome is NativeAllocationOutcome.OutOfMemory)
            {
                return false;
            }

            throw device.CreateNativeAllocationException(outcome, hresult, declaration.SizeInBytes);
        }

        ReadWriteTexture2D<T, TPixel> texture = new(
            device,
            d3D12Resource.Get(),
            width,
            height,
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON);

        owner.AttachResource(texture, d3D12Resource.Get(), TrackedResourceState.Common, declaration.SizeInBytes);

        ExternalProviderEndpoint<TView> endpoint = runtime.Domain.TryGetEndpoint<TView>()!;
        HANDLE sharedHandle = device.CreateSharedHandle((IUnknown*)d3D12Resource.Get());
        DomainOperationLease lease = default;

        try
        {
            DomainOperationStatus operationStatus = runtime.Domain.TryAcquireOperation(
                ExternalDomainReference.TransientOperation,
                owner.GetResourceRecord(0).Id,
                releaseExternalReferenceOnDispose: false,
                out lease,
                out _);

            if (operationStatus is not DomainOperationStatus.Acquired)
            {
                return false;
            }

            ExternalTextureDescriptor textureDescriptor = new()
            {
                Width = width,
                Height = height,
                Format = ExternalTextureFormat.Bgra8Unorm,
                ExternalUsage = descriptor.ExternalUsage,
                AlphaMode = descriptor.AlphaMode
            };

            TView view = endpoint.OpenSharedTexture(new BorrowedSharedHandle((nint)sharedHandle.Value), in textureDescriptor);

            owner.AttachExternalObject(0, view);
        }
        finally
        {
            _ = Windows.CloseHandle(sharedHandle);
        }

        using (lease)
        {
            owner.GetResourceRecord(0).Ownership = descriptor.InitialOwner is ComputeSharedTextureInitialOwner.External
                ? ExternalOwnershipState.ExternalAvailable
                : ExternalOwnershipState.ComputeAvailable;

            owner.CompleteConstruction();

            isCommitted = this.slotGate.TryCommitReplacement(activeSetId, bindingEpoch, preparedToken, out _);

            if (!isCommitted)
            {
                owner.ReleaseUnpublished();
            }
        }

        return true;
    }

    /// <summary>
    /// Detaches and releases a prepared generation that was not published.
    /// </summary>
    /// <param name="owner">The prepared generation to release.</param>
    /// <param name="preparedToken">The token the prepared generation was installed with.</param>
    private void AbortPreparedGeneration(ResourceGenerationOwner owner, ulong preparedToken)
    {
        _ = this.slotGate.TryAbortReplacement(preparedToken, out _);

        owner.ReleaseUnpublished();
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if the current slot is not bound to a resource set.
    /// </summary>
    private void ThrowIfNotBound()
    {
        default(InvalidOperationException).ThrowIf(
            this.slotGate.IsUnbound,
            "The shared texture slot is not bound to a resource set.");
    }
}
