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

        Assert.IsTrue(((IGenerationBoundResource)buffer).TryGetGenerationBinding(
            out ResourceGenerationSetHandle bufferSet,
            out uint bufferIndex,
            out ResourceGenerationId bufferGeneration));

        Assert.AreSame(buffer, bufferSet.Owner);
        Assert.AreEqual(0u, bufferIndex);
        Assert.AreEqual(1, bufferSet.Owner.ResourceCount);
        Assert.AreNotEqual(0ul, bufferSet.SetId.Value);
        Assert.AreNotEqual(0ul, bufferGeneration.Value);

        Assert.IsTrue(((IGenerationBoundResource)texture).TryGetGenerationBinding(
            out ResourceGenerationSetHandle textureSet,
            out _,
            out ResourceGenerationId textureGeneration));

        Assert.AreSame(texture, textureSet.Owner);
        Assert.AreNotEqual(bufferSet.SetId.Value, textureSet.SetId.Value);
        Assert.AreNotEqual(bufferGeneration.Value, textureGeneration.Value);

        Assert.AreEqual(
            ResourceGenerationState.Active,
            bufferSet.Owner.GetResourceRecord(0).ReadLifecycle());
        Assert.AreEqual(
            TrackedResourceState.Common,
            bufferSet.Owner.GetResourceRecord(0).D3D12State);
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

            Assert.IsTrue(((IGenerationBoundResource)binding.Resource!).TryGetGenerationBinding(
                out ResourceGenerationSetHandle set,
                out uint resourceIndex,
                out ResourceGenerationId generation));

            Assert.AreNotSame(binding.Resource, set.Owner);
            Assert.AreEqual(0u, resourceIndex);
            Assert.AreEqual(binding.SetId.Value, set.SetId.Value);
            Assert.AreEqual(binding.GenerationId.Value, generation.Value);
        }
        finally
        {
            host.Dispose();
        }
    }
}
