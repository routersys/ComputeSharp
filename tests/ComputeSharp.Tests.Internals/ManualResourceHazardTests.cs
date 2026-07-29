using System;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public partial class ManualResourceHazardTests
{
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct WriteShader : IComputeShader
    {
        private readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = ThreadIds.X;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct TextureWriteShader : IComputeShader
    {
        private readonly ReadWriteTexture2D<float> texture;

        public void Execute()
        {
            this.texture[ThreadIds.XY] = 1;
        }
    }

    [TestMethod]
    public void ManualComputeCommitsTheResourceFence()
    {
        GraphicsDevice device = GraphicsDevice.GetDefault();
        using ReadWriteBuffer<int> buffer = device.AllocateReadWriteBuffer<int>(64);
        using ComputeContext context = device.CreateComputeContext();

        context.For(64, new WriteShader(buffer));
        context.Submit();

        ref ResourceGenerationRecord record = ref ((IResourceGenerationOwner)buffer).GetResourceRecord(0);

        Assert.AreEqual(ComputeQueueKind.Compute, record.LastWrite.Queue);
        Assert.AreNotEqual(0ul, record.LastWrite.Value);
        Assert.IsTrue(record.LastComputeRead.IsNone);
        Assert.IsTrue(record.LastCopyRead.IsNone);
        Assert.AreEqual(TrackedResourceState.Common, record.D3D12State);
    }

    [TestMethod]
    public void ManualTransitionCommitsStateOnSubmission()
    {
        GraphicsDevice device = GraphicsDevice.GetDefault();
        using ReadWriteTexture2D<float> texture = device.AllocateReadWriteTexture2D<float>(16, 16);
        using ComputeContext context = device.CreateComputeContext();

        context.Transition(texture, ResourceState.ReadOnly);

        ref ResourceGenerationRecord record = ref ((IResourceGenerationOwner)texture).GetResourceRecord(0);

        Assert.AreEqual(TrackedResourceState.UnorderedAccess, record.D3D12State);

        context.Submit();

        Assert.AreEqual(ComputeQueueKind.Compute, record.LastWrite.Queue);
        Assert.AreNotEqual(0ul, record.LastWrite.Value);
        Assert.AreEqual(TrackedResourceState.NonPixelShaderResource, record.D3D12State);
    }

    [TestMethod]
    public void ManualBufferCopyCommitsCommonStateAndCopyFences()
    {
        GraphicsDevice device = GraphicsDevice.GetDefault();
        int[] values = [1, 2, 3, 4];
        using ReadWriteBuffer<int> source = device.AllocateReadWriteBuffer(values);
        using ReadWriteBuffer<int> destination = device.AllocateReadWriteBuffer<int>(values.Length);

        source.CopyTo(destination);

        ref ResourceGenerationRecord sourceRecord = ref ((IResourceGenerationOwner)source).GetResourceRecord(0);
        ref ResourceGenerationRecord destinationRecord = ref ((IResourceGenerationOwner)destination).GetResourceRecord(0);

        Assert.AreEqual(ComputeQueueKind.Copy, sourceRecord.LastCopyRead.Queue);
        Assert.AreNotEqual(0ul, sourceRecord.LastCopyRead.Value);
        Assert.AreEqual(TrackedResourceState.Common, sourceRecord.D3D12State);
        Assert.AreEqual(ComputeQueueKind.Copy, destinationRecord.LastWrite.Queue);
        Assert.AreNotEqual(0ul, destinationRecord.LastWrite.Value);
        Assert.IsTrue(destinationRecord.LastComputeRead.IsNone);
        Assert.IsTrue(destinationRecord.LastCopyRead.IsNone);
        Assert.AreEqual(TrackedResourceState.Common, destinationRecord.D3D12State);
        CollectionAssert.AreEqual(values, destination.ToArray());
    }

    [TestMethod]
    public void ManualWriteRevokesReadOnlyViewAvailability()
    {
        GraphicsDevice device = GraphicsDevice.GetDefault();
        using ReadWriteTexture2D<float> texture = device.AllocateReadWriteTexture2D<float>(16, 16);

        using (ComputeContext transitionContext = device.CreateComputeContext())
        {
            transitionContext.Transition(texture, ResourceState.ReadOnly);
        }

        Assert.IsNotNull(texture.AsReadOnly());

        using ComputeContext writeContext = device.CreateComputeContext();

        writeContext.For(16, 16, new TextureWriteShader(texture));

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => texture.AsReadOnly());
    }

    [TestMethod]
    public unsafe void OpenedSharedTextureHasGenerationIdentity()
    {
        GraphicsDevice device = GraphicsDevice.GetDefault();
        using ReadWriteTexture2D<float> source = InteropServices.AllocateSharedReadWriteTexture2D<float>(device, 16, 16);
        nint handle = InteropServices.CreateSharedHandle(source);

        try
        {
            using ReadWriteTexture2D<float> opened = InteropServices.OpenSharedReadWriteTexture2D<float>(device, handle);

            Assert.IsTrue(((IGenerationBoundResource)opened).TryGetGenerationBinding(out ResourceUsageBinding binding));
            Assert.AreNotEqual(0ul, binding.Generation.Value);
            Assert.IsFalse(binding.Set.IsEmpty);
            Assert.AreEqual(TrackedResourceState.Common, binding.Set.Owner.GetResourceRecord(0).D3D12State);
        }
        finally
        {
            Assert.IsTrue(Windows.CloseHandle(new HANDLE((void*)handle)));
        }
    }
}
