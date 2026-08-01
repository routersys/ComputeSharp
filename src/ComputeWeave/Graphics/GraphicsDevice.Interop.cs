using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;
using ComputeWeave.Win32;

namespace ComputeWeave;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// The capabilities every external interop provider has to declare.
    /// </summary>
    private const ExternalInteropCapabilities RequiredExternalInteropCapabilities =
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering;

    /// <summary>
    /// Registers an external interop provider on the current device.
    /// </summary>
    /// <typeparam name="TView">The type of the external view the provider creates over a shared texture.</typeparam>
    /// <param name="provider">The provider to register.</param>
    /// <returns>The <see cref="ComputeInteropDomain"/> instance owning <paramref name="provider"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="provider"/> runs on another adapter.</exception>
    /// <exception cref="NotSupportedException">Thrown if <paramref name="provider"/> lacks a required capability.</exception>
    /// <remarks>
    /// Ownership of <paramref name="provider"/> moves to the runtime as this method is entered. The returned domain
    /// owns it when the registration succeeds, and the runtime disposes it exactly once when the registration fails.
    /// A caller never disposes <paramref name="provider"/> itself, whether this method returns or throws.
    /// </remarks>
    public ComputeInteropDomain RegisterExternalDomain<TView>(IComputeExternalInteropProvider<TView> provider)
        where TView : class, IDisposable
    {
        default(ArgumentNullException).ThrowIfNull(provider);

        ExternalProviderEndpoint<TView>? endpoint = null;

        try
        {
            endpoint = new ExternalProviderEndpoint<TView>(provider);

            return RegisterExternalDomain(endpoint, nameof(provider));
        }
        catch
        {
            if (endpoint is null)
            {
                provider.Dispose();
            }
            else
            {
                endpoint.DisposeProvider();
            }

            throw;
        }
    }

    /// <summary>
    /// Registers the endpoint of an external interop provider the runtime already owns.
    /// </summary>
    /// <param name="endpoint">The endpoint of the provider to register.</param>
    /// <param name="parameterName">The name of the parameter the provider was passed as.</param>
    /// <returns>The <see cref="ComputeInteropDomain"/> instance owning <paramref name="endpoint"/>.</returns>
    private ComputeInteropDomain RegisterExternalDomain(ExternalProviderEndpoint endpoint, string parameterName)
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

        ThrowIfDeviceTerminal();

        default(ArgumentException).ThrowIf(endpoint.AdapterIdentity.AdapterLuid != Luid.ToInt64(), parameterName);

        ExternalInteropCapabilities capabilities = endpoint.Capabilities;

        if ((capabilities & RequiredExternalInteropCapabilities) != RequiredExternalInteropCapabilities)
        {
            throw new NotSupportedException(
                $"""The external interop provider does not declare the required "{RequiredExternalInteropCapabilities}" capabilities.""");
        }

        ComputeExternalQueueScheduler scheduler = endpoint.Scheduler;

        default(ArgumentException).ThrowIf(scheduler is null, parameterName);

        DeviceRegistrationRegistry registry = GetRegistrationRegistry();

        if (!SchedulerRegistration.TryAcquire(scheduler, out SchedulerRegistration? schedulerRegistration))
        {
            throw new InvalidOperationException("The external queue scheduler no longer accepts domain registrations.");
        }

        try
        {
            using ComPtr<ID3D12Fence> d3D12SharedFence = CreateSharedFence();

            HANDLE sharedFenceHandle = CreateSharedHandle((IUnknown*)d3D12SharedFence.Get());

            try
            {
                ExternalTimelineInitialization initialization = new(new BorrowedSharedHandle((nint)sharedFenceHandle.Value));

                endpoint.Initialize(in initialization);
            }
            finally
            {
                _ = Windows.CloseHandle(sharedFenceHandle);
            }

            ExternalDomainId id = registry.AllocateDomainId();

            ComputeInteropDomain domain = new(
                this,
                registry,
                id,
                capabilities,
                endpoint,
                schedulerRegistration,
                d3D12SharedFence.Move());

            try
            {
                registry.PublishDomain(domain);
            }
            catch
            {
                domain.Dispose();

                throw;
            }

            return domain;
        }
        catch
        {
            schedulerRegistration.Release();

            throw;
        }
    }
}
