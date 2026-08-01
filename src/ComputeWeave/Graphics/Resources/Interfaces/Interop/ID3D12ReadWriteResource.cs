using ComputeWeave.Interop;
using ComputeWeave.Win32;

namespace ComputeWeave.Resources.Interop;

/// <summary>
/// An interface for a writeable, non-generic graphics resource types.
/// </summary>
internal unsafe interface ID3D12ReadWriteResource : ID3D12ReadOnlyResource
{
    void SetReadOnlyViewAvailability(bool isAvailable);

    void ResetReadOnlyViewAvailability();

    /// <summary>
    /// Validates the given resource for usage with a specified device, and retrieves its GPU and CPU descriptor handles for a clear operation.
    /// </summary>
    /// <param name="device">The target <see cref="GraphicsDevice"/> instance in use.</param>
    /// <param name="isNormalized">Indicates whether the current resource uses a normalized format.</param>
    /// <returns>The GPU and CPU descriptor handles for the resource.</returns> 
    (D3D12_GPU_DESCRIPTOR_HANDLE Gpu, D3D12_CPU_DESCRIPTOR_HANDLE Cpu) ValidateAndGetGpuAndCpuDescriptorHandlesForClear(GraphicsDevice device, out bool isNormalized);

    /// <summary>
    /// Validates the given resource for usage with a specified device, and retrieves the underlying <see cref="ID3D12Resource"/> object, along with the target transition state.
    /// </summary>
    /// <param name="device">The target <see cref="GraphicsDevice"/> instance in use.</param>
    /// <param name="resourceState">The target state to transition the resource to.</param>
    /// <param name="d3D12Resource">The the underlying <see cref="ID3D12Resource"/> object.</param>
    /// <param name="lease">The <see cref="ReferenceTracker.Lease"/> value for the returned <see cref="ID3D12Resource"/> object.</param>
    /// <returns>The target resource state for <paramref name="d3D12Resource"/>.</returns>
    D3D12_RESOURCE_STATES ValidateAndGetID3D12ResourceAndTransitionState(
        GraphicsDevice device,
        ResourceState resourceState,
        out ID3D12Resource* d3D12Resource,
        out ReferenceTracker.Lease lease);
}
