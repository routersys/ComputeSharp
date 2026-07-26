using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

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
