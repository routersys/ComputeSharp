using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class SlotGateTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private readonly ResourceGenerationRecord[] records;

        public GenerationOwner(ulong setId, ulong generationId)
        {
            SetId = new ResourceGenerationSetId(setId);
            this.records =
            [
                new ResourceGenerationRecord
                {
                    Id = new ResourceGenerationId(generationId),
                    Lifecycle = ResourceGenerationState.Active,
                    OwnerReferenceCount = 1,
                    ExternalObjectsReleased = 1
                }
            ];
        }

        public ResourceGenerationSetId SetId { get; }

        public int ResourceCount => this.records.Length;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.records[resourceOrdinal];
        }
    }

    private static ResourceGenerationSetHandle Handle(ulong setId, ulong generationId)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(setId, generationId));
    }

    private static OwnedSlotDescriptor BufferSlot()
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Buffer,
            ComputeResourceRecovery.Discardable,
            new ResourcePlanFieldDescriptor[]
            {
                new(0, 0, "Member", "ComputeSharp.ReadWriteBuffer`1", "memberLength", ResourcePlanDimensionKind.Length)
            });
    }

    private sealed class GateHolder
    {
        public SlotGate Gate;
    }

    private static GateHolder BoundGate(out int[] storage)
    {
        GateHolder holder = new();

        storage = new int[3];

        Assert.IsTrue(holder.Gate.TryBind(storage, new SlotResourcePlanStateRecord(0, 1)));

        return holder;
    }

    private static GateHolder PublishedGate(out ResourceGenerationSetHandle active)
    {
        GateHolder holder = BoundGate(out _);

        active = Handle(1, 1);

        Assert.IsTrue(holder.Gate.TryInstallPrepared(active, 1, [16]));
        Assert.IsTrue(holder.Gate.TryCommitReplacement(default, 0, 1, out _));

        return holder;
    }

    [TestMethod]
    public void CreatesUnboundGate()
    {
        GateHolder holder = new();

        Assert.IsTrue(holder.Gate.IsUnbound);
        Assert.IsTrue(holder.Gate.IsDisposalComplete);
        Assert.IsFalse(holder.Gate.IsAllocated);
        Assert.IsFalse(holder.Gate.IsDisposeRequested);
        Assert.AreEqual(0UL, holder.Gate.GetBindingEpoch());
        Assert.IsFalse(holder.Gate.TryPin(new ResourceGenerationSetId(1), new ResourceGenerationId(1), 0, 0, out _));
    }

    [TestMethod]
    public void BindsOnlyOnce()
    {
        GateHolder holder = BoundGate(out int[] storage);

        Assert.IsFalse(holder.Gate.IsUnbound);
        Assert.IsFalse(holder.Gate.IsDisposalComplete);
        Assert.IsFalse(holder.Gate.TryBind(storage, new SlotResourcePlanStateRecord(0, 1)));
    }

    [TestMethod]
    public void RejectsPlanStateOutsideStorage()
    {
        GateHolder holder = new();

        _ = Assert.ThrowsException<ArgumentNullException>(() => holder.Gate.TryBind(null!, new SlotResourcePlanStateRecord(0, 1)));
        _ = Assert.ThrowsException<ArgumentException>(() => holder.Gate.TryBind(new int[2], new SlotResourcePlanStateRecord(0, 1)));
        _ = Assert.ThrowsException<ArgumentException>(() => holder.Gate.TryBind(new int[3], new SlotResourcePlanStateRecord(1, 1)));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => holder.Gate.TryBind(new int[3], new SlotResourcePlanStateRecord(-1, 1)));
    }

    [TestMethod]
    public void PublishesGenerationThroughGate()
    {
        GateHolder holder = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsTrue(holder.Gate.IsAllocated);
        Assert.AreEqual(1UL, holder.Gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, holder.Gate.Evaluate(BufferSlot(), [16]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, holder.Gate.Evaluate(BufferSlot(), [32]));
        Assert.IsTrue(holder.Gate.TryPin(active.SetId, new ResourceGenerationId(1), 1, 0, out ResourceGenerationPin pin));

        SlotControlRecord.ReleasePin(in pin);
    }

    [TestMethod]
    public void RejectsPinWithStaleBindingEpoch()
    {
        GateHolder holder = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsFalse(holder.Gate.TryPin(active.SetId, new ResourceGenerationId(1), 0, 0, out _));
        Assert.IsFalse(holder.Gate.TryPin(new ResourceGenerationSetId(2), new ResourceGenerationId(1), 1, 0, out _));
        Assert.IsFalse(holder.Gate.TryPin(active.SetId, new ResourceGenerationId(2), 1, 0, out _));
    }

    [TestMethod]
    public void AbortsPreparedReplacementThroughGate()
    {
        GateHolder holder = PublishedGate(out _);
        ResourceGenerationSetHandle prepared = Handle(2, 2);

        Assert.IsTrue(holder.Gate.TryInstallPrepared(prepared, 2, [32]));
        Assert.IsTrue(holder.Gate.TryAbortReplacement(2, out ResourceGenerationSetHandle detachedPrepared));
        Assert.AreEqual(prepared.SetId, detachedPrepared.SetId);
        Assert.AreEqual(1UL, holder.Gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, holder.Gate.Evaluate(BufferSlot(), [16]));
    }

    [TestMethod]
    public void AppliesLogicalUpdateThroughGate()
    {
        GateHolder holder = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsTrue(holder.Gate.TryApplyLogicalUpdate(active.SetId, 1, [8]));
        Assert.AreEqual(1UL, holder.Gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, holder.Gate.Evaluate(BufferSlot(), [8]));
        Assert.IsFalse(holder.Gate.TryApplyLogicalUpdate(active.SetId, 1, [32]));
    }

    [TestMethod]
    public void TrimsActiveGenerationThroughGate()
    {
        GateHolder holder = PublishedGate(out _);

        Assert.IsTrue(holder.Gate.TryTrim());
        Assert.IsFalse(holder.Gate.IsAllocated);
        Assert.AreEqual(2UL, holder.Gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Replacement, holder.Gate.Evaluate(BufferSlot(), [16]));
    }

    [TestMethod]
    public void RequestsDisposeThroughGate()
    {
        GateHolder holder = PublishedGate(out _);

        _ = holder.Gate.RequestDispose();

        Assert.IsTrue(holder.Gate.IsDisposeRequested);
        Assert.IsFalse(holder.Gate.IsDisposalComplete);
        Assert.IsFalse(holder.Gate.TryTrim());
    }

    [TestMethod]
    public void CompletesDisposalOfUnboundGate()
    {
        GateHolder holder = new();

        _ = holder.Gate.RequestDispose();

        Assert.IsTrue(holder.Gate.IsDisposeRequested);
        Assert.IsTrue(holder.Gate.IsDisposalComplete);
        Assert.IsFalse(holder.Gate.TryBind(new int[3], new SlotResourcePlanStateRecord(0, 1)));
    }

    [TestMethod]
    public void MarksDeviceTerminalThroughGate()
    {
        GateHolder holder = PublishedGate(out _);

        Assert.IsTrue(holder.Gate.TryMarkDeviceTerminal());
        Assert.IsTrue(holder.Gate.IsDisposeRequested);
        Assert.IsFalse(holder.Gate.TryCompleteRetiringActive());
    }
}
