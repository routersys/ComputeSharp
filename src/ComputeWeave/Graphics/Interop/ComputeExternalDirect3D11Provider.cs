using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Win32;
using static ComputeWeave.Win32.D2D1_ALPHA_MODE;
using static ComputeWeave.Win32.D2D1_BITMAP_OPTIONS;
using static ComputeWeave.Win32.DXGI_FORMAT;

#pragma warning disable CA1063

namespace ComputeWeave;

/// <summary>
/// An interop provider enqueueing onto a Direct3D11 immediate context.
/// </summary>
/// <remarks>
/// <para>
/// The provider takes the device, the immediate context and the render target as raw COM pointers, so that it
/// stays independent of the Direct3D11 bindings the caller uses. It queries the interfaces it needs from them,
/// and releases those references when it is disposed.
/// </para>
/// <para>
/// The caller owns the scheduler and releases it after every domain built on it. The provider never releases
/// it. One scheduler corresponds to exactly one immediate context, and the caller keeps that mapping: every
/// provider enqueueing onto the same context is constructed with the same scheduler instance.
/// </para>
/// </remarks>
public unsafe class ComputeExternalDirect3D11Provider : IComputeExternalInteropProvider<ExternalDirect3D11TextureView>
{
    /// <summary>
    /// The <c>ID3D11Device1</c> object opening shared textures.
    /// </summary>
    private ComPtr<ID3D11Device1> device1;

    /// <summary>
    /// The <c>ID3D11Device5</c> object opening the shared fence.
    /// </summary>
    private ComPtr<ID3D11Device5> device5;

    /// <summary>
    /// The <c>ID3D11DeviceContext4</c> object the external queue work is enqueued onto.
    /// </summary>
    private ComPtr<ID3D11DeviceContext4> context;

    /// <summary>
    /// The <c>ID2D1DeviceContext</c> object creating the bitmaps of external views, if any.
    /// </summary>
    private ComPtr<ID2D1DeviceContext> renderTarget;

    /// <summary>
    /// The <c>ID3D11Fence</c> object of the shared timeline, once initialized.
    /// </summary>
    private ComPtr<ID3D11Fence> fence;

    /// <summary>
    /// The scheduler serializing the reservations of the immediate context.
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
    /// Creates a new <see cref="ComputeExternalDirect3D11Provider"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <c>ID3D11Device</c> object to enqueue against.</param>
    /// <param name="immediateContext">The <c>ID3D11DeviceContext</c> object to enqueue onto.</param>
    /// <param name="renderTarget">
    /// The <c>ID2D1DeviceContext</c> object creating the bitmaps of external views, or <c>0</c> to create views
    /// carrying no bitmap.
    /// </param>
    /// <param name="scheduler">The scheduler of <paramref name="immediateContext"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="device"/> or <paramref name="immediateContext"/> is <c>0</c>.</exception>
    /// <remarks>
    /// The adapter identity is read from <paramref name="device"/>. It is not taken from the caller, so that it
    /// cannot disagree with the device the work is enqueued against.
    /// </remarks>
    public ComputeExternalDirect3D11Provider(
        nint device,
        nint immediateContext,
        nint renderTarget,
        ComputeExternalQueueScheduler scheduler)
    {
        default(ArgumentNullException).ThrowIfNull(scheduler);
        default(ArgumentException).ThrowIf(device == 0, nameof(device));
        default(ArgumentException).ThrowIf(immediateContext == 0, nameof(immediateContext));

        this.scheduler = scheduler;

        try
        {
            using ComPtr<IUnknown> deviceUnknown = new((IUnknown*)device);
            using ComPtr<IUnknown> contextUnknown = new((IUnknown*)immediateContext);

            deviceUnknown.CopyTo(ref this.device1).Assert();
            deviceUnknown.CopyTo(ref this.device5).Assert();
            contextUnknown.CopyTo(ref this.context).Assert();

            if (renderTarget != 0)
            {
                using ComPtr<IUnknown> renderTargetUnknown = new((IUnknown*)renderTarget);

                renderTargetUnknown.CopyTo(ref this.renderTarget).Assert();
            }

            this.adapterIdentity = GetAdapterIdentity(deviceUnknown.Get());
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
    /// The persistent external view ordering is not offered. It states that external views keep their order
    /// across the lifetime of a domain, and the provider cannot observe that.
    /// </remarks>
    public ExternalInteropCapabilities Capabilities =>
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is already initialized.</exception>
    public void Initialize(in ExternalTimelineInitialization initialization)
    {
        default(InvalidOperationException).ThrowIf(this.fence.Get() is not null, "The provider is already initialized.");

        ID3D11Fence* fence = null;

        this.device5.Get()->OpenSharedFence(
            (HANDLE)initialization.SharedFenceHandle.DangerousGetHandle(),
            Windows.__uuidof<ID3D11Fence>(),
            (void**)&fence).Assert();

        this.fence.Attach(fence);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is not initialized.</exception>
    public void EnqueueSignal(ulong value)
    {
        ID3D11Fence* fence = this.fence.Get();

        default(InvalidOperationException).ThrowIf(fence is null, "The provider is not initialized.");

        this.context.Get()->Signal(fence, value).Assert();
    }

    /// <inheritdoc/>
    public void FlushAfterSignal()
    {
        this.context.Get()->Flush();
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown if the current provider is not initialized.</exception>
    public void EnqueueWait(ulong value)
    {
        ID3D11Fence* fence = this.fence.Get();

        default(InvalidOperationException).ThrowIf(fence is null, "The provider is not initialized.");

        this.context.Get()->Wait(fence, value).Assert();
    }

    /// <inheritdoc/>
    public ExternalDirect3D11TextureView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        using ComPtr<ID3D11Texture2D> texture = default;

        this.device1.Get()->OpenSharedResource1(
            (HANDLE)resourceHandle.DangerousGetHandle(),
            Windows.__uuidof<ID3D11Texture2D>(),
            (void**)texture.GetAddressOf()).Assert();

        using ComPtr<ID2D1Bitmap1> bitmap = default;

        if (this.renderTarget.Get() is not null)
        {
            using ComPtr<IDXGISurface> surface = default;

            texture.CopyTo(surface.GetAddressOf()).Assert();

            D2D1_BITMAP_PROPERTIES1 properties = new()
            {
                pixelFormat = new D2D1_PIXEL_FORMAT
                {
                    format = DXGI_FORMAT_B8G8R8A8_UNORM,
                    alphaMode = GetAlphaMode(descriptor.AlphaMode)
                },
                dpiX = 96.0f,
                dpiY = 96.0f,
                bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
                colorContext = null
            };

            this.renderTarget.Get()->CreateBitmapFromDxgiSurface(
                surface.Get(),
                &properties,
                bitmap.GetAddressOf()).Assert();
        }

        return new ExternalDirect3D11TextureView(texture.Detach(), bitmap.Detach());
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
        this.renderTarget.Dispose();
        this.context.Dispose();
        this.device5.Dispose();
        this.device1.Dispose();
    }

    /// <summary>
    /// Reads the adapter identity of a device.
    /// </summary>
    /// <param name="device">The <c>IUnknown</c> object of the device to read.</param>
    /// <returns>The identity of the adapter <paramref name="device"/> runs on.</returns>
    private static ExternalAdapterIdentity GetAdapterIdentity(IUnknown* device)
    {
        using ComPtr<IDXGIDevice> dxgiDevice = default;

        device->QueryInterface(Windows.__uuidof<IDXGIDevice>(), (void**)dxgiDevice.GetAddressOf()).Assert();

        using ComPtr<IDXGIAdapter> adapter = default;

        dxgiDevice.Get()->GetAdapter(adapter.GetAddressOf()).Assert();

        DXGI_ADAPTER_DESC description;

        adapter.Get()->GetDesc(&description).Assert();

        long adapterLuid = ((long)description.AdapterLuid.HighPart << 32) | description.AdapterLuid.LowPart;

        return new ExternalAdapterIdentity(adapterLuid);
    }

    /// <summary>
    /// Maps an alpha mode to the Direct2D one.
    /// </summary>
    /// <param name="alphaMode">The alpha mode to map.</param>
    /// <returns>The Direct2D alpha mode matching <paramref name="alphaMode"/>.</returns>
    /// <remarks>
    /// The two enumerations do not share their values. Casting one to the other maps <c>Ignore</c> to the
    /// unknown mode.
    /// </remarks>
    private static D2D1_ALPHA_MODE GetAlphaMode(ComputeAlphaMode alphaMode)
    {
        return alphaMode switch
        {
            ComputeAlphaMode.Premultiplied => D2D1_ALPHA_MODE_PREMULTIPLIED,
            ComputeAlphaMode.Straight => D2D1_ALPHA_MODE_STRAIGHT,
            _ => D2D1_ALPHA_MODE_IGNORE
        };
    }
}
