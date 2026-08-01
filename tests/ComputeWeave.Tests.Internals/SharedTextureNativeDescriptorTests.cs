using ComputeWeave.Graphics.Extensions;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Plans;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class SharedTextureNativeDescriptorTests
{
    private static GraphicsCommittedResourceDescription Describe(
        GraphicsDevice device,
        int width,
        int height)
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.DescribeInteropSharedTexture(
                device,
                width,
                height,
                out ComputeGenerationDeclaration declaration));

        Assert.AreEqual(ComputeGenerationShape.Texture2D, declaration.Shape);
        Assert.AreEqual(width, declaration.Width);
        Assert.AreEqual(height, declaration.Height);
        Assert.AreNotEqual(0ul, declaration.SizeInBytes);

        return declaration.Description;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void FixesEveryFieldOfTheSharedTextureNativeDescriptor(Device device)
    {
        GraphicsCommittedResourceDescription description = Describe(device.Get(), 64, 32);

        Assert.AreEqual(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT, description.HeapProperties.Type);
        Assert.AreEqual(D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_SHARED, description.HeapFlags);
        Assert.AreEqual(D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON, description.ResourceStates);

        D3D12_RESOURCE_DESC resourceDescription = description.ResourceDescription;

        Assert.AreEqual(D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D, resourceDescription.Dimension);
        Assert.AreEqual(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, resourceDescription.Format);
        Assert.AreEqual(64ul, resourceDescription.Width);
        Assert.AreEqual(32u, resourceDescription.Height);
        Assert.AreEqual((ushort)1, resourceDescription.DepthOrArraySize);
        Assert.AreEqual((ushort)1, resourceDescription.MipLevels);
        Assert.AreEqual(1u, resourceDescription.SampleDesc.Count);
        Assert.AreEqual(0u, resourceDescription.SampleDesc.Quality);
        Assert.AreEqual(D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN, resourceDescription.Layout);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void NeverAllowsCrossAdapterSharing(Device device)
    {
        D3D12_RESOURCE_FLAGS flags = Describe(device.Get(), 16, 16).ResourceDescription.Flags;

        Assert.AreEqual(D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, flags & D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_CROSS_ADAPTER);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AlwaysAllowsUnorderedAccessRenderTargetAndSimultaneousAccess(Device device)
    {
        D3D12_RESOURCE_FLAGS flags = Describe(device.Get(), 16, 16).ResourceDescription.Flags;

        Assert.AreNotEqual(D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, flags & D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS);
        Assert.AreNotEqual(D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, flags & D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET);
        Assert.AreNotEqual(D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, flags & D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CreatesTheDescribedSharedTextureOnTheDevice(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        GraphicsCommittedResourceDescription description = Describe(graphicsDevice, 32, 16);

        HRESULT hresult = ID3D12DeviceExtensions.CreateCommittedResource(
            ref *graphicsDevice.D3D12Device,
            in description,
            out ComPtr<ID3D12Resource> created3D12Resource);

        using ComPtr<ID3D12Resource> d3D12Resource = created3D12Resource;

        Assert.IsTrue(hresult >= 0);

        D3D12_RESOURCE_DESC created = d3D12Resource.Get()->GetDesc();

        Assert.AreEqual(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, created.Format);
        Assert.AreEqual(32ul, created.Width);
        Assert.AreEqual(16u, created.Height);
        Assert.AreEqual((ushort)1, created.MipLevels);
        Assert.AreEqual(D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, created.Flags & D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_CROSS_ADAPTER);

        HANDLE sharedHandle = graphicsDevice.CreateSharedHandle((IUnknown*)d3D12Resource.Get());

        Assert.AreNotEqual(0, (nint)sharedHandle.Value);
        Assert.IsTrue(Windows.CloseHandle(sharedHandle));
    }
}
