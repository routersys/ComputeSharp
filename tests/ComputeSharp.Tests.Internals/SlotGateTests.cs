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

    private static SlotGate BoundGate(out int[] storage)
    {
        SlotGate gate = new();

        storage = new int[3];

        Assert.IsTrue(gate.TryBind(storage, new SlotResourcePlanStateRecord(0, 1)));

        return gate;
    }

    private static SlotGate PublishedGate(out ResourceGenerationSetHandle active)
    {
        SlotGate gate = BoundGate(out _);

        active = Handle(1, 1);

        Assert.IsTrue(gate.TryInstallPrepared(active, 1, [16]));
        Assert.IsTrue(gate.TryCommitReplacement(default, 0, 1, out _));

        return gate;
    }

    [TestMethod]
    public void CreatesUnboundGate()
    {
        SlotGate gate = new();

        Assert.IsTrue(gate.IsUnbound);
        Assert.IsTrue(gate.IsDisposalComplete);
        Assert.IsFalse(gate.IsAllocated);
        Assert.IsFalse(gate.IsDisposeRequested);
        Assert.AreEqual(0UL, gate.GetBindingEpoch());
        Assert.IsFalse(gate.TryPin(new ResourceGenerationSetId(1), new ResourceGenerationId(1), 0, 0, out _));
    }

    [TestMethod]
    public void BindsOnlyOnce()
    {
        SlotGate gate = BoundGate(out int[] storage);

        Assert.IsFalse(gate.IsUnbound);
        Assert.IsFalse(gate.IsDisposalComplete);
        Assert.IsFalse(gate.TryBind(storage, new SlotResourcePlanStateRecord(0, 1)));
    }

    [TestMethod]
    public void RejectsPlanStateOutsideStorage()
    {
        SlotGate gate = new();

        _ = Assert.ThrowsException<ArgumentNullException>(() => gate.TryBind(null!, new SlotResourcePlanStateRecord(0, 1)));
        _ = Assert.ThrowsException<ArgumentException>(() => gate.TryBind(new int[2], new SlotResourcePlanStateRecord(0, 1)));
        _ = Assert.ThrowsException<ArgumentException>(() => gate.TryBind(new int[3], new SlotResourcePlanStateRecord(1, 1)));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => gate.TryBind(new int[3], new SlotResourcePlanStateRecord(-1, 1)));
    }

    [TestMethod]
    public void PublishesGenerationThroughGate()
    {
        SlotGate gate = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsTrue(gate.IsAllocated);
        Assert.AreEqual(1UL, gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, gate.Evaluate(BufferSlot(), [16]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, gate.Evaluate(BufferSlot(), [32]));
        Assert.IsTrue(gate.TryPin(active.SetId, new ResourceGenerationId(1), 1, 0, out ResourceGenerationPin pin));

        SlotControlRecord.ReleasePin(in pin);
    }

    [TestMethod]
    public void RejectsPinWithStaleBindingEpoch()
    {
        SlotGate gate = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsFalse(gate.TryPin(active.SetId, new ResourceGenerationId(1), 0, 0, out _));
        Assert.IsFalse(gate.TryPin(new ResourceGenerationSetId(2), new ResourceGenerationId(1), 1, 0, out _));
        Assert.IsFalse(gate.TryPin(active.SetId, new ResourceGenerationId(2), 1, 0, out _));
    }

    [TestMethod]
    public void AbortsPreparedReplacementThroughGate()
    {
        SlotGate gate = PublishedGate(out _);
        ResourceGenerationSetHandle prepared = Handle(2, 2);

        Assert.IsTrue(gate.TryInstallPrepared(prepared, 2, [32]));
        Assert.IsTrue(gate.TryAbortReplacement(2, out ResourceGenerationSetHandle detachedPrepared));
        Assert.AreEqual(prepared.SetId, detachedPrepared.SetId);
        Assert.AreEqual(1UL, gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, gate.Evaluate(BufferSlot(), [16]));
    }

    [TestMethod]
    public void AppliesLogicalUpdateThroughGate()
    {
        SlotGate gate = PublishedGate(out ResourceGenerationSetHandle active);

        Assert.IsTrue(gate.TryApplyLogicalUpdate(active.SetId, 1, [8]));
        Assert.AreEqual(1UL, gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Identical, gate.Evaluate(BufferSlot(), [8]));
        Assert.IsFalse(gate.TryApplyLogicalUpdate(active.SetId, 1, [32]));
    }

    [TestMethod]
    public void TrimsActiveGenerationThroughGate()
    {
        SlotGate gate = PublishedGate(out _);

        Assert.IsTrue(gate.TryTrim());
        Assert.IsFalse(gate.IsAllocated);
        Assert.AreEqual(2UL, gate.GetBindingEpoch());
        Assert.AreEqual(ResourcePlanDecision.Replacement, gate.Evaluate(BufferSlot(), [16]));
    }

    [TestMethod]
    public void RequestsDisposeThroughGate()
    {
        SlotGate gate = PublishedGate(out _);

        _ = gate.RequestDispose();

        Assert.IsTrue(gate.IsDisposeRequested);
        Assert.IsFalse(gate.IsDisposalComplete);
        Assert.IsFalse(gate.TryTrim());
    }

    [TestMethod]
    public void CompletesDisposalOfUnboundGate()
    {
        SlotGate gate = new();

        _ = gate.RequestDispose();

        Assert.IsTrue(gate.IsDisposeRequested);
        Assert.IsTrue(gate.IsDisposalComplete);
        Assert.IsFalse(gate.TryBind(new int[3], new SlotResourcePlanStateRecord(0, 1)));
    }

    [TestMethod]
    public void MarksDeviceTerminalThroughGate()
    {
        SlotGate gate = PublishedGate(out _);

        Assert.IsTrue(gate.TryMarkDeviceTerminal());
        Assert.IsTrue(gate.IsDisposeRequested);
        Assert.IsFalse(gate.TryCompleteRetiringActive());
    }
}
