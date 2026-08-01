using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceGenerationBindingTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void OwnsTheGenerationOfADirectlyAllocatedResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteTexture2D<float> texture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        Assert.IsTrue(((IGenerationBoundResource)buffer).TryGetGenerationBinding(out ResourceUsageBinding bufferBinding));

        Assert.AreSame(buffer, bufferBinding.Set.Owner);
        Assert.AreEqual(0u, bufferBinding.ResourceIndex);
        Assert.AreEqual(1, bufferBinding.Set.Owner.ResourceCount);
        Assert.AreNotEqual(0ul, bufferBinding.Set.SetId.Value);
        Assert.AreNotEqual(0ul, bufferBinding.Generation.Value);

        Assert.IsTrue(((IGenerationBoundResource)texture).TryGetGenerationBinding(out ResourceUsageBinding textureBinding));

        Assert.AreSame(texture, textureBinding.Set.Owner);
        Assert.AreNotEqual(bufferBinding.Set.SetId.Value, textureBinding.Set.SetId.Value);
        Assert.AreNotEqual(bufferBinding.Generation.Value, textureBinding.Generation.Value);

        Assert.AreEqual(
            ResourceGenerationState.Active,
            bufferBinding.Set.Owner.GetResourceRecord(0).ReadLifecycle());
        Assert.AreEqual(
            TrackedResourceState.Common,
            bufferBinding.Set.Owner.GetResourceRecord(0).D3D12State);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DerivesTheObservedUsageOfEveryDirectlyAllocatedResourceKind(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using ConstantBuffer<int> constantBuffer = graphicsDevice.AllocateConstantBuffer<int>(16);
        using ReadOnlyBuffer<int> readOnlyBuffer = graphicsDevice.AllocateReadOnlyBuffer<int>(16);
        using ReadWriteBuffer<int> readWriteBuffer = graphicsDevice.AllocateReadWriteBuffer<int>(16);
        using ReadOnlyTexture2D<float> readOnlyTexture = graphicsDevice.AllocateReadOnlyTexture2D<float>(8, 8);
        using ReadWriteTexture2D<float> readWriteTexture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        AssertUsage(constantBuffer, ComputeResourceAccess.Read, TrackedResourceState.GenericRead);
        AssertUsage(readOnlyBuffer, ComputeResourceAccess.Read, TrackedResourceState.Common);
        AssertUsage(readWriteBuffer, ComputeResourceAccess.ReadWrite, TrackedResourceState.Common);
        AssertUsage(readOnlyTexture, ComputeResourceAccess.Read, TrackedResourceState.Common);
        AssertUsage(readWriteTexture, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess);
    }

    private static void AssertUsage(IGraphicsResource resource, ComputeResourceAccess access, TrackedResourceState residentState)
    {
        Assert.IsTrue(((IGenerationBoundResource)resource).TryGetGenerationBinding(out ResourceUsageBinding binding));

        Assert.AreEqual(access, binding.Access);
        Assert.AreEqual(residentState, binding.ResidentState);
        Assert.AreEqual(residentState, binding.Set.Owner.GetResourceRecord((int)binding.ResourceIndex).D3D12State);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void BindsTheGenerationOfAPlanProducedResourceToItsOwner(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            ComputeHostRuntimeTests.CreateDescriptor(ResourcePlanKind.Buffer),
            1,
            [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [128], new ComputeHostRuntimeTests.BufferMaterializer(128), out _));

            ComputeResourceBinding<ReadWriteBuffer<int>> binding = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.IsTrue(binding.IsValid);

            Assert.IsTrue(((IGenerationBoundResource)binding.Resource!).TryGetGenerationBinding(out ResourceUsageBinding usageBinding));

            Assert.AreNotSame(binding.Resource, usageBinding.Set.Owner);
            Assert.AreEqual(0u, usageBinding.ResourceIndex);
            Assert.AreEqual(binding.SetId.Value, usageBinding.Set.SetId.Value);
            Assert.AreEqual(binding.GenerationId.Value, usageBinding.Generation.Value);
        }
        finally
        {
            host.Dispose();
        }
    }
}
