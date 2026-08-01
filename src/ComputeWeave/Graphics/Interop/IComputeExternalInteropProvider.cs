using System;

namespace ComputeWeave;

/// <summary>
/// The single endpoint an external graphics API exposes to a compute interop domain.
/// </summary>
/// <typeparam name="TView">The type of the external view the provider creates over a shared texture.</typeparam>
/// <remarks>
/// A provider never generates, changes or reuses a timeline value, and never reenters the domain, the
/// scheduler or the graphics device from one of its own calls.
/// </remarks>
public interface IComputeExternalInteropProvider<TView> : IDisposable
    where TView : class, IDisposable
{
    /// <summary>
    /// Gets the identity of the adapter the current provider runs on.
    /// </summary>
    ExternalAdapterIdentity AdapterIdentity { get; }

    /// <summary>
    /// Gets the scheduler of the immediate context the current provider enqueues onto.
    /// </summary>
    /// <remarks>
    /// Providers sharing one immediate context return the same instance by reference equality.
    /// </remarks>
    ComputeExternalQueueScheduler Scheduler { get; }

    /// <summary>
    /// Gets the capabilities the current provider offers.
    /// </summary>
    ExternalInteropCapabilities Capabilities { get; }

    /// <summary>
    /// Initializes the current provider against the timeline of the domain it is registered into.
    /// </summary>
    /// <param name="initialization">The timeline information of the domain.</param>
    void Initialize(in ExternalTimelineInitialization initialization);

    /// <summary>
    /// Enqueues a signal of the shared timeline onto the external queue.
    /// </summary>
    /// <param name="value">The timeline value to signal.</param>
    void EnqueueSignal(ulong value);

    /// <summary>
    /// Flushes the external queue so that a previously enqueued signal becomes observable.
    /// </summary>
    void FlushAfterSignal();

    /// <summary>
    /// Enqueues a wait for the shared timeline onto the external queue.
    /// </summary>
    /// <param name="value">The timeline value to wait for.</param>
    void EnqueueWait(ulong value);

    /// <summary>
    /// Opens a shared texture and creates the external view over it.
    /// </summary>
    /// <param name="resourceHandle">The borrowed shared NT handle of the texture to open.</param>
    /// <param name="descriptor">The description of the texture to open.</param>
    /// <returns>The external view over the opened texture.</returns>
    TView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor);

    /// <summary>
    /// Notifies the current provider that the graphics device reached its terminal state.
    /// </summary>
    /// <param name="reason">The reason the graphics device is terminal.</param>
    /// <remarks>
    /// This runs exactly once per provider, outside any runtime gate and any scheduler reservation. An
    /// exception thrown from here is saved as a diagnostic and does not interrupt the teardown.
    /// </remarks>
    void OnDeviceTerminal(Exception reason);
}
