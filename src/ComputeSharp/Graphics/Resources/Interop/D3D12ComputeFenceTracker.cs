using System.Threading;

namespace ComputeSharp.Resources.Interop;

internal struct D3D12ComputeFenceTracker
{
    private ulong d3D12FenceValue;

    public ulong Value => Volatile.Read(ref this.d3D12FenceValue);

    public void Mark(ulong d3D12FenceValue)
    {
        ulong currentValue;

        while ((currentValue = Volatile.Read(ref this.d3D12FenceValue)) < d3D12FenceValue &&
               Interlocked.CompareExchange(ref this.d3D12FenceValue, d3D12FenceValue, currentValue) != currentValue)
        {
        }
    }
}
