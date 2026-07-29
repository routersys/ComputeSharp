using ComputeSharp.Resources.Lifetime;
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
}
