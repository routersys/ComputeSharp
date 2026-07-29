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
