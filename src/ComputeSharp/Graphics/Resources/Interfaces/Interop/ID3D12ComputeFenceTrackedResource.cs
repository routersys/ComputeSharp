namespace ComputeSharp.Resources.Interop;

internal interface ID3D12ComputeFenceTrackedResource
{
    void MarkComputeFence(ulong d3D12FenceValue);
}
