using System;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceSlotUnboundStateTests
{
    private sealed class ExternalView : IDisposable
    {
        public void Dispose()
        {
        }
    }

    [TestMethod]
    public void CreatesUnboundResourceSlot()
    {
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();

        Assert.IsFalse(slot.IsAllocated);
        Assert.IsFalse(slot.IsDisposeRequested);

        slot.WaitForDisposal();
    }

    [TestMethod]
    public void DisposesUnboundResourceSlotIdempotently()
    {
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();

        slot.Dispose();

        Assert.IsTrue(slot.IsDisposeRequested);
        Assert.IsFalse(slot.IsAllocated);

        slot.Dispose();

        Assert.IsTrue(slot.IsDisposeRequested);

        slot.WaitForDisposal();
    }

    [TestMethod]
    public void CreatesUnboundResourceGroupSlot()
    {
        ComputeResourceGroupSlot<object> slot = new();

        Assert.IsFalse(slot.IsAllocated);
        Assert.IsFalse(slot.IsDisposeRequested);

        slot.WaitForDisposal();
        slot.Dispose();
        slot.Dispose();

        Assert.IsTrue(slot.IsDisposeRequested);

        slot.WaitForDisposal();
    }

    [TestMethod]
    public void CreatesUnboundSharedTextureSlot()
    {
        SharedTextureSlot<Bgra32, float4, ExternalView> slot = new();

        Assert.AreEqual(0, slot.Width);
        Assert.AreEqual(0, slot.Height);
        Assert.IsFalse(slot.IsAllocated);
        Assert.IsFalse(slot.IsDisposeRequested);

        slot.WaitForDisposal();
        slot.Dispose();
        slot.Dispose();

        Assert.IsTrue(slot.IsDisposeRequested);

        slot.WaitForDisposal();
    }

    [TestMethod]
    public void RejectsUnboundSharedTextureSlotOperations()
    {
        SharedTextureSlot<Bgra32, float4, ExternalView> slot = new();

        _ = Assert.ThrowsException<InvalidOperationException>(() => slot.TryEnsure(1, 1, out _));
        _ = Assert.ThrowsException<InvalidOperationException>(() => slot.GetComputeBinding());
        _ = Assert.ThrowsException<InvalidOperationException>(slot.AcquireExternalViewLease);
    }

    [TestMethod]
    public void RejectsNonPositiveSharedTextureDimensionsBeforeStateCheck()
    {
        SharedTextureSlot<Bgra32, float4, ExternalView> slot = new();

        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => slot.TryEnsure(0, 1, out _));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => slot.TryEnsure(1, -1, out _));
    }

    [TestMethod]
    public void TreatsDefaultResourceBindingAsInvalid()
    {
        ComputeResourceBinding<ReadWriteBuffer<int>> binding = default;

        Assert.IsFalse(binding.IsValid);
        Assert.IsNull(binding.Resource);
        Assert.AreEqual(default(ResourceGenerationSetId), binding.SetId);
        Assert.AreEqual(default(ResourceGenerationId), binding.GenerationId);
        Assert.AreEqual(0ul, binding.BindingEpoch);
        Assert.AreEqual(0, binding.ResourceIndex);
    }
}
