using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Win32;

#pragma warning disable CA1063

namespace ComputeWeave;

/// <summary>
/// An interop provider enqueueing onto a Direct3D12 command queue of its own device.
/// </summary>
/// <remarks>
/// <para>
/// The provider takes the device and the command queue as raw COM pointers, so that it stays independent of
/// the Direct3D12 bindings the caller uses. It queries the interfaces it needs from them, and releases those
/// references when it is disposed.
/// </para>
/// <para>
/// The caller owns the scheduler and releases it after every domain built on it. The provider never releases
/// it. One scheduler corresponds to exactly one command queue, and the caller keeps that mapping: every
/// provider enqueueing onto the same queue is constructed with the same scheduler instance.
/// </para>
/// </remarks>
public unsafe class ComputeExternalDirect3D12Provider : IComputeExternalInteropProvider<ExternalDirect3D12TextureView>
{
    /// <summary>
    /// The <c>ID3D12Device</c> object opening the shared fence and the shared textures.
    /// </summary>
    private ComPtr<ID3D12Device> device;

    /// <summary>
    /// The <c>ID3D12CommandQueue</c> object the external queue work is enqueued onto.
    /// </summary>
    private ComPtr<ID3D12CommandQueue> queue;

    /// <summary>
    /// The <c>ID3D12Fence</c> object of the shared timeline, once initialized.
    /// </summary>
    private ComPtr<ID3D12Fence> fence;

    /// <summary>
    /// The scheduler serializing the reservations of the command queue.
    /// </summary>
    private readonly ComputeExternalQueueScheduler scheduler;

    /// <summary>
    /// The identity of the adapter the device runs on.
    /// </summary>
    private readonly ExternalAdapterIdentity adapterIdentity;

    /// <summary>
    /// Whether <see cref="Dispose"/> has run.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Creates a new <see cref="ComputeExternalDirect3D12Provider"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <c>ID3D12Device</c> object to enqueue against.</param>
    /// <param name="queue">The <c>ID3D12CommandQueue</c> object to enqueue onto.</param>
    /// <param name="scheduler">The scheduler of <paramref name="queue"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="device"/> or <paramref name="queue"/> is <c>0</c>.</exception>
    /// <remarks>
    /// The adapter identity is read from <paramref name="device"/>. It is not taken from the caller, so that it
    /// cannot disagree with the device the work is enqueued against.
    /// </remarks>
    public ComputeExternalDirect3D12Provider(
        nint device,
        nint queue,
        ComputeExternalQueueScheduler scheduler)
    {
        default(ArgumentNullException).ThrowIfNull(scheduler);
        default(ArgumentException).ThrowIf(device == 0, nameof(device));
        default(ArgumentException).ThrowIf(queue == 0, nameof(queue));

        this.scheduler = scheduler;

        try
        {
            using ComPtr<IUnknown> deviceUnknown = new((IUnknown*)device);
            using ComPtr<IUnknown> queueUnknown = new((IUnknown*)queue);

            deviceUnknown.CopyTo(ref this.device).Assert();
            queueUnknown.CopyTo(ref this.queue).Assert();

            LUID adapterLuid = this.device.Get()->GetAdapterLuid();

            this.adapterIdentity = new ExternalAdapterIdentity(((long)adapterLuid.HighPart << 32) | adapterLuid.LowPart);
        }
        catch
        {
            ReleaseInterfaces();

            throw;
        }
    }

    /// <inheritdoc/>
    public ExternalAdapterIdentity AdapterIdentity => this.adapterIdentity;

    /// <inheritdoc/>
    public ComputeExternalQueueScheduler Scheduler => this.scheduler;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// The single immediate context ordering names the guarantee that all external work of the domain is
    /// enqueued into one totally ordered external stream. For this provider that stream is the one command
    /// queue: everything it signals, waits and executes runs in submission order.
    /// </para>
    /// <para>
    /// The persistent external view ordering follows from the same construction: every view is opened from
    /// the shared texture of the domain, and all external work against it is enqueued onto the one command
    /// queue whose reservations the scheduler serializes. A view therefore keeps its order while it is held.
    /// </para>
    /// </remarks>
    public ExternalInteropCapabilities Capabilities =>
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering |
        ExternalInteropCapabilities.PersistentExternalViewOrdering;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is already initialized.</exception>
    public void Initialize(in ExternalTimelineInitialization initialization)
    {
        default(InvalidOperationException).ThrowIf(this.fence.Get() is not null, "The provider is already initialized.");

        ID3D12Fence* fence = null;

        this.device.Get()->OpenSharedHandle(
            (HANDLE)initialization.SharedFenceHandle.DangerousGetHandle(),
            Windows.__uuidof<ID3D12Fence>(),
            (void**)&fence).Assert();

        this.fence.Attach(fence);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is not initialized.</exception>
    public void EnqueueSignal(ulong value)
    {
        ID3D12Fence* fence = this.fence.Get();

        default(InvalidOperationException).ThrowIf(fence is null, "The provider is not initialized.");

        this.queue.Get()->Signal(fence, value).Assert();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This does nothing. A Direct3D12 command queue has no deferred batching to flush: a signal enqueued onto
    /// it is already submitted when the call returns.
    /// </remarks>
    public void FlushAfterSignal()
    {
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is not initialized.</exception>
    public void EnqueueWait(ulong value)
    {
        ID3D12Fence* fence = this.fence.Get();

        default(InvalidOperationException).ThrowIf(fence is null, "The provider is not initialized.");

        this.queue.Get()->Wait(fence, value).Assert();
    }

    /// <inheritdoc/>
    public ExternalDirect3D12TextureView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        using ComPtr<ID3D12Resource> resource = default;

        this.device.Get()->OpenSharedHandle(
            (HANDLE)resourceHandle.DangerousGetHandle(),
            Windows.__uuidof<ID3D12Resource>(),
            (void**)resource.GetAddressOf()).Assert();

        return new ExternalDirect3D12TextureView(resource.Detach());
    }

    /// <inheritdoc/>
    public void OnDeviceTerminal(Exception reason)
    {
        OnDeviceTerminalCore(reason);
    }

    /// <summary>
    /// Handles the graphics device reaching its terminal state.
    /// </summary>
    /// <param name="reason">The reason the graphics device is terminal.</param>
    /// <remarks>
    /// This runs exactly once per provider. The base implementation does nothing. An exception thrown from an
    /// override is saved as a diagnostic and does not interrupt the teardown.
    /// </remarks>
    protected virtual void OnDeviceTerminalCore(Exception reason)
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;

        DisposeCore();
        ReleaseInterfaces();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources an override owns, before the queried interfaces are released.
    /// </summary>
    /// <remarks>
    /// The scheduler is not released here, and must not be released by an override. The caller owns it.
    /// </remarks>
    protected virtual void DisposeCore()
    {
    }

    /// <summary>
    /// Releases every interface the current provider holds.
    /// </summary>
    private void ReleaseInterfaces()
    {
        this.fence.Dispose();
        this.queue.Dispose();
        this.device.Dispose();
    }
}
