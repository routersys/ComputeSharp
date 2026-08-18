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
