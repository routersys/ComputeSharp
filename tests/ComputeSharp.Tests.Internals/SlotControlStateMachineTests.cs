using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class SlotControlStateMachineTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private readonly ResourceGenerationRecord[] records;

        public GenerationOwner(ulong setId, ulong generationId, int resourceCount = 1)
        {
            SetId = new ResourceGenerationSetId(setId);
            this.records = new ResourceGenerationRecord[resourceCount];

            for (int i = 0; i < resourceCount; i++)
            {
                this.records[i] = new ResourceGenerationRecord
                {
                    Id = new ResourceGenerationId(generationId),
                    Lifecycle = ResourceGenerationState.Active,
                    OwnerReferenceCount = 1,
                    ExternalObjectsReleased = 1
                };
            }
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
        return new ResourceGenerationSetHandle(new ResourceGenerationSetId(setId), new GenerationOwner(setId, generationId));
    }

    private static SlotControlRecord BoundSlot(out ResourceGenerationSetHandle active)
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        active = Handle(1, 1);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1));

        return slot;
    }

    [TestMethod]
    public void BindsOnlyOnce()
    {
        SlotControlRecord slot = default;

        Assert.AreEqual(SlotControlState.Unbound, slot.State);
        Assert.IsTrue(slot.TryBind());
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.IsFalse(slot.TryBind());
    }

    [TestMethod]
    public void PublishesInitialGenerationAndIncrementsEpoch()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.AreEqual(1ul, slot.BindingEpoch);
        Assert.AreEqual(active.SetId, slot.Active.SetId);
        Assert.IsTrue(slot.Retired.IsEmpty);
    }

    [TestMethod]
    public void PinsOnlyMatchingGenerationAndEpoch()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0));
        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(2), slot.BindingEpoch, 0));
        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch + 1, 0));
        Assert.IsFalse(slot.TryPin(new ResourceGenerationSetId(9), new ResourceGenerationId(1), slot.BindingEpoch, 0));
    }

    [TestMethod]
    public void ReleasesPinThroughOwner()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0));
        Assert.AreEqual(1, slot.Active.Owner.GetResourceRecord(0).RecordingReferenceCount);

        slot.ReleasePin(0);

        Assert.AreEqual(0, slot.Active.Owner.GetResourceRecord(0).RecordingReferenceCount);
    }

    [TestMethod]
    public void RejectsPinAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        slot.RequestDispose();

        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0));
    }

    [TestMethod]
    public void RejectsSecondPreparedInstall()
    {
        SlotControlRecord slot = BoundSlot(out _);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.AreEqual(SlotControlState.ReplacementPrepared, slot.State);
        Assert.IsFalse(slot.TryInstallPrepared(Handle(3, 3), 3));
    }

    [TestMethod]
    public void RejectsPreparedInstallWithZeroToken()
    {
        SlotControlRecord slot = BoundSlot(out _);

        Assert.IsFalse(slot.TryInstallPrepared(Handle(2, 2), 0));
    }

    [TestMethod]
    public void CommitsReplacementAndRetiresActive()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2));
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.AreEqual(replacement.SetId, slot.Active.SetId);
        Assert.AreEqual(active.SetId, slot.Retired.SetId);
        Assert.AreEqual(2ul, slot.BindingEpoch);
        Assert.AreEqual(0ul, slot.PreparedToken);
    }

    [TestMethod]
    public void RejectsCommitOnStaleEpoch()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsFalse(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch + 1, 2));
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.AreEqual(active.SetId, slot.Active.SetId);
        Assert.AreEqual(1ul, slot.BindingEpoch);
        Assert.IsTrue(slot.Prepared.IsEmpty);
    }

    [TestMethod]
    public void RejectsCommitOnStaleToken()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsFalse(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 99));
        Assert.AreEqual(active.SetId, slot.Active.SetId);
    }

    [TestMethod]
    public void RejectsCommitOnStaleActiveSet()
    {
        SlotControlRecord slot = BoundSlot(out _);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsFalse(slot.TryCommitReplacement(new ResourceGenerationSetId(9), slot.BindingEpoch, 2));
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void SkipsTrimWhenRetiredPresent()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2));
        Assert.IsFalse(slot.TryTrim());
    }

    [TestMethod]
    public void TrimsActiveAndIncrementsEpoch()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryTrim());
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.IsTrue(slot.Active.IsEmpty);
        Assert.AreEqual(active.SetId, slot.Retired.SetId);
        Assert.AreEqual(2ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void DisposesUnboundSlotDirectly()
    {
        SlotControlRecord slot = default;

        slot.RequestDispose();

        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void DisposesSlotWithoutActiveDirectly()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        slot.RequestDispose();

        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void MovesToRetiringActiveWhenRetiredAbsent()
    {
        SlotControlRecord slot = BoundSlot(out _);

        slot.RequestDispose();

        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void WaitsForRetiredBeforeRetiringActive()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2));

        slot.RequestDispose();

        Assert.AreEqual(SlotControlState.DisposeWaitingForRetired, slot.State);
        Assert.IsFalse(slot.TryCompleteRetiringActive());
        Assert.IsTrue(slot.TryClearRetired());
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void DetachesPreparedOnDispose()
    {
        SlotControlRecord slot = BoundSlot(out _);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));

        slot.RequestDispose();

        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(0ul, slot.PreparedToken);
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
    }

    [TestMethod]
    public void RejectsCommitAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));

        ulong epoch = slot.BindingEpoch;

        slot.RequestDispose();

        Assert.IsFalse(slot.TryCommitReplacement(active.SetId, epoch, 2));
        Assert.AreEqual(active.SetId, slot.Active.SetId);
    }

    [TestMethod]
    public void RejectsPreparedInstallAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out _);

        slot.RequestDispose();

        Assert.IsFalse(slot.TryInstallPrepared(Handle(2, 2), 2));
    }

    [TestMethod]
    public void MovesAnyNonDisposedStateToRetiringActiveOnDeviceTerminal()
    {
        SlotControlRecord slot = BoundSlot(out _);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsTrue(slot.TryMarkDeviceTerminal());
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.IsTrue(slot.Prepared.IsEmpty);

        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.IsFalse(slot.TryMarkDeviceTerminal());
    }
}
