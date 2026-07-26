using ComputeSharp.Win32;

namespace ComputeSharp.Memory;

internal readonly struct GraphicsCommittedResourceDescription(
    D3D12_HEAP_PROPERTIES heapProperties,
    D3D12_HEAP_FLAGS heapFlags,
    D3D12_RESOURCE_DESC resourceDescription,
    D3D12_RESOURCE_STATES resourceStates)
{
    public D3D12_HEAP_PROPERTIES HeapProperties { get; } = heapProperties;

    public D3D12_HEAP_FLAGS HeapFlags { get; } = heapFlags;

    public D3D12_RESOURCE_DESC ResourceDescription { get; } = resourceDescription;

    public D3D12_RESOURCE_STATES ResourceStates { get; } = resourceStates;
}
