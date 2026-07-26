using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Creates a 2D texture resource that can be shared across APIs.
    /// </summary>
    /// <param name="resourceType">The resource type of the resource to create.</param>
    /// <param name="dxgiFormat">The <see cref="DXGI_FORMAT"/> value to use.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <param name="isRenderTarget">Whether the texture can be used as a render target.</param>
    /// <param name="d3D12Resource">The created <see cref="ID3D12Resource"/> object.</param>
    /// <param name="d3D12ResourceStates">The initial <see cref="D3D12_RESOURCE_STATES"/> value for the created resource.</param>
    /// <returns>The memory accounting of the created resource.</returns>
    internal GraphicsMemoryAllocation CreateSharedResource(
        ResourceType resourceType,
        DXGI_FORMAT dxgiFormat,
        uint width,
        uint height,
        bool isRenderTarget,
        out ComPtr<ID3D12Resource> d3D12Resource,
        out D3D12_RESOURCE_STATES d3D12ResourceStates)
    {
        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetSharedCommittedResourceDescription(
            resourceType,
            dxgiFormat,
            width,
            height,
            isRenderTarget);

        d3D12ResourceStates = description.ResourceStates;

        return AllocateCommittedResource(in description, out d3D12Resource);
    }

    /// <summary>
    /// Creates a shared NT handle for the specified COM object.
    /// </summary>
    /// <param name="pObject">The COM object to create the shared handle for.</param>
    /// <returns>The created shared NT handle.</returns>
    internal HANDLE CreateSharedHandle(IUnknown* pObject)
    {
        return this.d3D12Device.Get()->CreateSharedHandle(pObject);
    }

    /// <summary>
    /// Opens an <see cref="ID3D12Resource"/> from the specified shared NT handle.
    /// </summary>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <returns>A reference to the opened <see cref="ID3D12Resource"/>.</returns>
    internal ComPtr<ID3D12Resource> OpenSharedResource(HANDLE handle)
    {
        return this.d3D12Device.Get()->OpenSharedHandle<ID3D12Resource>(handle);
    }

    /// <summary>
    /// Creates an <see cref="ID3D12Fence"/> that can be shared with other APIs.
    /// </summary>
    /// <returns>A reference to the shareable <see cref="ID3D12Fence"/> created.</returns>
    internal ComPtr<ID3D12Fence> CreateSharedFence()
    {
        return this.d3D12Device.Get()->CreateSharedFence();
    }

    /// <summary>
    /// Opens an <see cref="ID3D12Fence"/> from the specified shared NT handle.
    /// </summary>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <returns>A reference to the opened <see cref="ID3D12Fence"/>.</returns>
    internal ComPtr<ID3D12Fence> OpenSharedFence(HANDLE handle)
    {
        return this.d3D12Device.Get()->OpenSharedHandle<ID3D12Fence>(handle);
    }

    /// <summary>
    /// Signals the specified shared fence with a value on the compute queue.
    /// </summary>
    /// <param name="d3D12Fence">The target <see cref="ID3D12Fence"/>.</param>
    /// <param name="value">The value to signal.</param>
    internal void SignalSharedFence(ID3D12Fence* d3D12Fence, ulong value)
    {
        lock (this.d3D12ComputeCommandQueueLock)
        {
            this.d3D12ComputeCommandQueue.Get()->Signal(d3D12Fence, value).Assert();
        }
    }

    /// <summary>
    /// Enqueues a wait on the compute queue until the specified shared fence reaches the target value.
    /// </summary>
    /// <param name="d3D12Fence">The target <see cref="ID3D12Fence"/>.</param>
    /// <param name="value">The target value to wait for.</param>
    internal void WaitForSharedFence(ID3D12Fence* d3D12Fence, ulong value)
    {
        lock (this.d3D12ComputeCommandQueueLock)
        {
            this.d3D12ComputeCommandQueue.Get()->Wait(d3D12Fence, value).Assert();
        }
    }
}
