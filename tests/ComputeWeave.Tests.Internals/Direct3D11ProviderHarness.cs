using System;
using System.Threading;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

#pragma warning disable CA2213

namespace ComputeWeave.Tests.Internals;

internal sealed unsafe class Direct3D11ExternalQueueScheduler : ComputeExternalQueueScheduler
{
    private int entered;

    public int DisposeCount;

    protected override void EnterCore()
    {
        if (Interlocked.CompareExchange(ref this.entered, 1, 0) != 0)
        {
            throw new InvalidOperationException("External queue scheduler is busy or reentered.");
        }
    }

    protected override void ExitCore()
    {
        if (Interlocked.Exchange(ref this.entered, 0) != 1)
        {
            throw new InvalidOperationException("Scheduler exit invariant failed.");
        }
    }

    protected override void DisposeCore()
    {
        this.DisposeCount++;
    }
}

internal sealed unsafe class Direct3D11ExternalView : IDisposable
{
    private ComPtr<ID3D11Texture2D> d3D11Texture;

    public Direct3D11ExternalView(ID3D11Texture2D* d3D11Texture, int width, int height)
    {
        this.d3D11Texture.Attach(d3D11Texture);

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int DisposeCount { get; private set; }

    public ID3D11Texture2D* D3D11Texture => this.d3D11Texture.Get();

    public void Dispose()
    {
        DisposeCount++;

        this.d3D11Texture.Dispose();
    }
}

internal sealed unsafe class Direct3D11ImmediateContext : IDisposable
{
    private ComPtr<ID3D11Device5> d3D11Device;

    private ComPtr<ID3D11DeviceContext4> d3D11ImmediateContext;

    private Direct3D11ImmediateContext(
        ID3D11Device5* d3D11Device,
        ID3D11DeviceContext4* d3D11ImmediateContext,
        long adapterLuid)
    {
        this.d3D11Device.Attach(d3D11Device);
        this.d3D11ImmediateContext.Attach(d3D11ImmediateContext);

        AdapterLuid = adapterLuid;
        Scheduler = new Direct3D11ExternalQueueScheduler();
    }

    public long AdapterLuid { get; }

    public Direct3D11ExternalQueueScheduler Scheduler { get; }

    public ID3D11Device5* D3D11Device => this.d3D11Device.Get();

    public ID3D11DeviceContext4* D3D11ImmediateContext => this.d3D11ImmediateContext.Get();

    public static Direct3D11ImmediateContext Create(long adapterLuid)
    {
        using ComPtr<IDXGIFactory4> dxgiFactory = default;
        using ComPtr<IDXGIAdapter> dxgiAdapter = default;
        using ComPtr<ID3D11Device> d3D11Device = default;
        using ComPtr<ID3D11DeviceContext> d3D11ImmediateContext = default;
        using ComPtr<ID3D11Device5> d3D11Device5 = default;
        using ComPtr<ID3D11DeviceContext4> d3D11ImmediateContext4 = default;

        ThrowIfFailed(DirectX.CreateDXGIFactory1(Windows.__uuidof<IDXGIFactory4>(), (void**)dxgiFactory.GetAddressOf()));

        LUID luid = new()
        {
            LowPart = (uint)adapterLuid,
            HighPart = (int)(adapterLuid >> 32)
        };

        ThrowIfFailed(dxgiFactory.Get()->EnumAdapterByLuid(luid, Windows.__uuidof<IDXGIAdapter>(), (void**)dxgiAdapter.GetAddressOf()));

        D3D_FEATURE_LEVEL featureLevel = D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1;

        ThrowIfFailed(DirectX.D3D11CreateDevice(
            dxgiAdapter.Get(),
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
            HMODULE.NULL,
            (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            &featureLevel,
            1,
            D3D11.D3D11_SDK_VERSION,
            d3D11Device.GetAddressOf(),
            null,
            d3D11ImmediateContext.GetAddressOf()));

        ThrowIfFailed(d3D11Device.CopyTo(d3D11Device5.GetAddressOf()));
        ThrowIfFailed(d3D11ImmediateContext.CopyTo(d3D11ImmediateContext4.GetAddressOf()));

        return new Direct3D11ImmediateContext(d3D11Device5.Detach(), d3D11ImmediateContext4.Detach(), adapterLuid);
    }

    public uint[] ReadBack(ID3D11Texture2D* d3D11Texture, int width, int height)
    {
        using ComPtr<ID3D11Texture2D> staging = default;

        D3D11_TEXTURE2D_DESC stagingDescription = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
            MiscFlags = 0
        };

        ThrowIfFailed(this.d3D11Device.Get()->CreateTexture2D(&stagingDescription, null, staging.GetAddressOf()));

        this.d3D11ImmediateContext.Get()->CopyResource((ID3D11Resource*)staging.Get(), (ID3D11Resource*)d3D11Texture);
        this.d3D11ImmediateContext.Get()->Flush();

        D3D11_MAPPED_SUBRESOURCE mapped;

        ThrowIfFailed(this.d3D11ImmediateContext.Get()->Map((ID3D11Resource*)staging.Get(), 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped));

        uint[] pixels = new uint[width * height];

        try
        {
            for (int y = 0; y < height; y++)
            {
                uint* row = (uint*)((byte*)mapped.pData + ((nuint)y * mapped.RowPitch));

                for (int x = 0; x < width; x++)
                {
                    pixels[(y * width) + x] = row[x];
                }
            }
        }
        finally
        {
            this.d3D11ImmediateContext.Get()->Unmap((ID3D11Resource*)staging.Get(), 0);
        }

        return pixels;
    }

    public void Write(ID3D11Texture2D* d3D11Texture, ReadOnlySpan<uint> pixels, int width, int height)
    {
        fixed (uint* source = pixels)
        {
            D3D11_SUBRESOURCE_DATA initialData = new()
            {
                pSysMem = source,
                SysMemPitch = (uint)(width * sizeof(uint)),
                SysMemSlicePitch = 0
            };

            D3D11_TEXTURE2D_DESC stagingDescription = new()
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = 0,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };

            using ComPtr<ID3D11Texture2D> staging = default;

            ThrowIfFailed(this.d3D11Device.Get()->CreateTexture2D(&stagingDescription, &initialData, staging.GetAddressOf()));

            this.d3D11ImmediateContext.Get()->CopyResource((ID3D11Resource*)d3D11Texture, (ID3D11Resource*)staging.Get());
            this.d3D11ImmediateContext.Get()->Flush();
        }
    }

    public void Dispose()
    {
        this.d3D11ImmediateContext.Dispose();
        this.d3D11Device.Dispose();

        Scheduler.Dispose();
    }

    private static void ThrowIfFailed(HRESULT hresult)
    {
        if (hresult.FAILED)
        {
            throw new InvalidOperationException($"The Direct3D 11 harness call failed with code 0x{(uint)hresult:X8}.");
        }
    }
}

internal sealed unsafe class Direct3D11InteropProvider(Direct3D11ImmediateContext context)
    : IComputeExternalInteropProvider<Direct3D11ExternalView>
{
    private readonly Direct3D11ImmediateContext context = context;

    private ComPtr<ID3D11Fence> d3D11SharedFence;

    public ExternalAdapterIdentity AdapterIdentity => new(this.context.AdapterLuid);

    public ComputeExternalQueueScheduler Scheduler => this.context.Scheduler;

    public ExternalInteropCapabilities Capabilities =>
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering |
        ExternalInteropCapabilities.PersistentExternalViewOrdering;

    public bool OpenedSharedFence => this.d3D11SharedFence.Get() is not null;

    public int DisposeCount { get; private set; }

    public void Initialize(in ExternalTimelineInitialization initialization)
    {
        nint handle = initialization.SharedFenceHandle.DangerousGetHandle();

        HRESULT hresult = this.context.D3D11Device->OpenSharedFence(
            (HANDLE)handle,
            Windows.__uuidof<ID3D11Fence>(),
            (void**)this.d3D11SharedFence.GetAddressOf());

        if (hresult.FAILED)
        {
            throw new InvalidOperationException($"The provider could not open the shared fence, code 0x{(uint)hresult:X8}.");
        }
    }

    public void EnqueueSignal(ulong value)
    {
        _ = this.context.D3D11ImmediateContext->Signal(this.d3D11SharedFence.Get(), value);
    }

    public void FlushAfterSignal()
    {
        this.context.D3D11ImmediateContext->Flush();
    }

    public void EnqueueWait(ulong value)
    {
        _ = this.context.D3D11ImmediateContext->Wait(this.d3D11SharedFence.Get(), value);
    }

    public Direct3D11ExternalView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        using ComPtr<ID3D11Texture2D> d3D11Texture = default;

        HRESULT hresult = this.context.D3D11Device->OpenSharedResource1(
            (HANDLE)resourceHandle.DangerousGetHandle(),
            Windows.__uuidof<ID3D11Texture2D>(),
            (void**)d3D11Texture.GetAddressOf());

        if (hresult.FAILED)
        {
            throw new InvalidOperationException($"The provider could not open the shared texture, code 0x{(uint)hresult:X8}.");
        }

        D3D11_TEXTURE2D_DESC textureDescription;

        d3D11Texture.Get()->GetDesc(&textureDescription);

        if (textureDescription.Width != (uint)descriptor.Width || textureDescription.Height != (uint)descriptor.Height)
        {
            throw new InvalidOperationException("The opened shared texture does not match the requested dimensions.");
        }

        if (textureDescription.Format != DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
        {
            throw new InvalidOperationException($"The opened shared texture has an unexpected format {textureDescription.Format}.");
        }

        LastOpenedView = new Direct3D11ExternalView(d3D11Texture.Detach(), descriptor.Width, descriptor.Height);
        OpenSharedTextureCount++;

        return LastOpenedView;
    }

    public Direct3D11ExternalView? LastOpenedView { get; private set; }

    public int OpenSharedTextureCount { get; private set; }

    public void OnDeviceTerminal(Exception reason)
    {
    }

    public void Dispose()
    {
        DisposeCount++;

        this.d3D11SharedFence.Dispose();
    }
}
