using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.DirectX;
using D3D12 = ComputeWeave.Win32;
using TxComPtr = TerraFX.Interop.Windows.ComPtr<TerraFX.Interop.DirectX.ID3D11Texture2D>;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class Direct3D11SharedTextureFlagTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Direct3D11RejectsAnUnorderedAccessSharedTextureWithoutRenderTargetAndSimultaneousAccess(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = Direct3D11ImmediateContext.Create(graphicsDevice.Luid.ToInt64());

        Assert.IsFalse(CanOpen(graphicsDevice, context, D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS));

        Assert.IsFalse(CanOpen(
            graphicsDevice,
            context,
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS |
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET));

        Assert.IsFalse(CanOpen(
            graphicsDevice,
            context,
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS |
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Direct3D11OpensAnUnorderedAccessSharedTextureWithRenderTargetAndSimultaneousAccess(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = Direct3D11ImmediateContext.Create(graphicsDevice.Luid.ToInt64());

        Assert.IsTrue(CanOpen(
            graphicsDevice,
            context,
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS |
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET |
            D3D12.D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS));
    }

    private static bool CanOpen(
        GraphicsDevice graphicsDevice,
        Direct3D11ImmediateContext context,
        D3D12.D3D12_RESOURCE_FLAGS resourceFlags)
    {
        D3D12.D3D12_HEAP_PROPERTIES heapProperties;
        heapProperties.Type = D3D12.D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT;
        heapProperties.CPUPageProperty = D3D12.D3D12_CPU_PAGE_PROPERTY.D3D12_CPU_PAGE_PROPERTY_UNKNOWN;
        heapProperties.MemoryPoolPreference = D3D12.D3D12_MEMORY_POOL.D3D12_MEMORY_POOL_UNKNOWN;
        heapProperties.CreationNodeMask = 1;
        heapProperties.VisibleNodeMask = 1;

        D3D12.D3D12_RESOURCE_DESC resourceDescription = D3D12.D3D12_RESOURCE_DESC.Tex2D(
            D3D12.DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            32,
            16,
            arraySize: 1,
            mipLevels: 1,
            sampleCount: 1,
            sampleQuality: 0,
            flags: resourceFlags,
            layout: D3D12.D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN);

        using D3D12.ComPtr<D3D12.ID3D12Resource> d3D12Resource = default;

        Assert.IsTrue(graphicsDevice.D3D12Device->CreateCommittedResource(
            &heapProperties,
            D3D12.D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_SHARED,
            &resourceDescription,
            D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON,
            null,
            D3D12.Windows.__uuidof<D3D12.ID3D12Resource>(),
            (void**)d3D12Resource.GetAddressOf()) >= 0);

        D3D12.HANDLE sharedHandle;

        Assert.IsTrue(graphicsDevice.D3D12Device->CreateSharedHandle(
            (D3D12.IUnknown*)d3D12Resource.Get(),
            null,
            D3D12.Windows.GENERIC_ALL,
            null,
            &sharedHandle) >= 0);

        try
        {
            using TxComPtr opened = default;

            return context.D3D11Device->OpenSharedResource1(
                (TerraFX.Interop.Windows.HANDLE)sharedHandle.Value,
                TerraFX.Interop.Windows.Windows.__uuidof<ID3D11Texture2D>(),
                (void**)opened.GetAddressOf()).SUCCEEDED;
        }
        finally
        {
            _ = D3D12.Windows.CloseHandle(sharedHandle);
        }
    }
}
