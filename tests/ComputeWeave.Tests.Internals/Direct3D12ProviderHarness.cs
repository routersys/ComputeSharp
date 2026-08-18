using System;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

#pragma warning disable CA2213

namespace ComputeWeave.Tests.Internals;

internal sealed unsafe class Direct3D12ExternalQueue : IDisposable
{
    private ComPtr<ID3D12Device> d3D12Device;

    private ComPtr<ID3D12CommandQueue> d3D12Queue;

    private Direct3D12ExternalQueue(ID3D12Device* d3D12Device, ID3D12CommandQueue* d3D12Queue, long adapterLuid)
    {
        this.d3D12Device.Attach(d3D12Device);
        this.d3D12Queue.Attach(d3D12Queue);

        AdapterLuid = adapterLuid;
    }

    public long AdapterLuid { get; }

    public ID3D12Device* D3D12Device => this.d3D12Device.Get();

    public ID3D12CommandQueue* D3D12Queue => this.d3D12Queue.Get();

    public static Direct3D12ExternalQueue Create(long adapterLuid)
    {
        using ComPtr<IDXGIFactory4> dxgiFactory = default;
        using ComPtr<IDXGIAdapter> dxgiAdapter = default;
        using ComPtr<ID3D12Device> d3D12Device = default;
        using ComPtr<ID3D12CommandQueue> d3D12Queue = default;

        ThrowIfFailed(DirectX.CreateDXGIFactory1(Windows.__uuidof<IDXGIFactory4>(), (void**)dxgiFactory.GetAddressOf()));

        LUID luid = new()
        {
            LowPart = (uint)adapterLuid,
            HighPart = (int)(adapterLuid >> 32)
        };

        ThrowIfFailed(dxgiFactory.Get()->EnumAdapterByLuid(luid, Windows.__uuidof<IDXGIAdapter>(), (void**)dxgiAdapter.GetAddressOf()));

        ThrowIfFailed(DirectX.D3D12CreateDevice(
            (IUnknown*)dxgiAdapter.Get(),
            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
            Windows.__uuidof<ID3D12Device>(),
            (void**)d3D12Device.GetAddressOf()));

        D3D12_COMMAND_QUEUE_DESC queueDescription = new()
        {
            Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT
        };

        ThrowIfFailed(d3D12Device.Get()->CreateCommandQueue(
            &queueDescription,
            Windows.__uuidof<ID3D12CommandQueue>(),
            (void**)d3D12Queue.GetAddressOf()));

        return new Direct3D12ExternalQueue(d3D12Device.Detach(), d3D12Queue.Detach(), adapterLuid);
    }

    public void Write(nint resource, ReadOnlySpan<uint> pixels, int width, int height)
    {
        uint rowPitch = (uint)(((width * sizeof(uint)) + (D3D12.D3D12_TEXTURE_DATA_PITCH_ALIGNMENT - 1)) & ~(D3D12.D3D12_TEXTURE_DATA_PITCH_ALIGNMENT - 1));
        ulong uploadLength = (ulong)rowPitch * (uint)height;

        using ComPtr<ID3D12Resource> upload = default;

        D3D12_HEAP_PROPERTIES heapProperties = new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD);
        D3D12_RESOURCE_DESC uploadDescription = D3D12_RESOURCE_DESC.Buffer(uploadLength);

        ThrowIfFailed(this.d3D12Device.Get()->CreateCommittedResource(
            &heapProperties,
            D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
            &uploadDescription,
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
            null,
            Windows.__uuidof<ID3D12Resource>(),
            (void**)upload.GetAddressOf()));

        void* mapped;

        ThrowIfFailed(upload.Get()->Map(0, null, &mapped));

        fixed (uint* source = pixels)
        {
            for (int y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(
                    source + ((nint)y * width),
                    (byte*)mapped + ((nuint)y * rowPitch),
                    rowPitch,
                    (uint)(width * sizeof(uint)));
            }
        }

        upload.Get()->Unmap(0, null);

        using ComPtr<ID3D12CommandAllocator> allocator = default;
        using ComPtr<ID3D12GraphicsCommandList> list = default;

        ThrowIfFailed(this.d3D12Device.Get()->CreateCommandAllocator(
            D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
            Windows.__uuidof<ID3D12CommandAllocator>(),
            (void**)allocator.GetAddressOf()));
        ThrowIfFailed(this.d3D12Device.Get()->CreateCommandList(
            0,
            D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
            allocator.Get(),
            null,
            Windows.__uuidof<ID3D12GraphicsCommandList>(),
            (void**)list.GetAddressOf()));

        D3D12_PLACED_SUBRESOURCE_FOOTPRINT footprint = new()
        {
            Offset = 0,
            Footprint = new D3D12_SUBRESOURCE_FOOTPRINT
            {
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                Width = (uint)width,
                Height = (uint)height,
                Depth = 1,
                RowPitch = rowPitch
            }
        };

        D3D12_TEXTURE_COPY_LOCATION source2 = new(upload.Get(), footprint);
        D3D12_TEXTURE_COPY_LOCATION destination = new((ID3D12Resource*)resource, 0);

        list.Get()->CopyTextureRegion(&destination, 0, 0, 0, &source2, null);

        ThrowIfFailed(list.Get()->Close());

        ID3D12CommandList* lists = (ID3D12CommandList*)list.Get();

        this.d3D12Queue.Get()->ExecuteCommandLists(1, &lists);

        WaitForIdle();
    }

    private void WaitForIdle()
    {
        using ComPtr<ID3D12Fence> fence = default;

        ThrowIfFailed(this.d3D12Device.Get()->CreateFence(
            0,
            D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE,
            Windows.__uuidof<ID3D12Fence>(),
            (void**)fence.GetAddressOf()));
        ThrowIfFailed(this.d3D12Queue.Get()->Signal(fence.Get(), 1));

        if (fence.Get()->GetCompletedValue() < 1)
        {
            ThrowIfFailed(fence.Get()->SetEventOnCompletion(1, HANDLE.NULL));
        }
    }

    public void Dispose()
    {
        this.d3D12Queue.Dispose();
        this.d3D12Device.Dispose();
    }

    private static void ThrowIfFailed(HRESULT hresult)
    {
        if (hresult.FAILED)
        {
            throw new InvalidOperationException($"The Direct3D 12 harness call failed with code 0x{(uint)hresult:X8}.");
        }
    }
}
