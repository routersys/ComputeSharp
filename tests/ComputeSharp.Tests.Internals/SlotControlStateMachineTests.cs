using System;
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

    private static ResourceGenerationSetHandle Handle(ulong setId, ulong generationId, int resourceCount = 1)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(setId, generationId, resourceCount));
    }

    private static void ReleaseAll(ResourceGenerationSetHandle handle)
    {
        for (int i = 0; i < handle.Owner.ResourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref handle.Owner.GetResourceRecord(i);

            record.Lifecycle = ResourceGenerationState.RetiredReady;

            Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
            Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        }
    }

    private static SlotControlRecord BoundSlot(out ResourceGenerationSetHandle active)
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        active = Handle(1, 1);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        return slot;
    }

    private static void AssertAllRetireRequested(ResourceGenerationSetHandle handle)
    {
        for (int i = 0; i < handle.Owner.ResourceCount; i++)
        {
            Assert.AreEqual(ResourceGenerationState.RetireRequested, handle.Owner.GetResourceRecord(i).Lifecycle);
        }
    }

    private static void AssertAllOwnerReferencesReleased(ResourceGenerationSetHandle handle)
    {
        for (int i = 0; i < handle.Owner.ResourceCount; i++)
        {
            Assert.AreEqual(0, handle.Owner.GetResourceRecord(i).OwnerReferenceCount);
        }
    }

    [TestMethod]
    public void ReleasesOwnerReferenceOfReplacedGenerationExactlyOnce()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        ResourceGenerationSetHandle replacement = Handle(2, 2, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));

        AssertAllOwnerReferencesReleased(active);

        for (int i = 0; i < replacement.Owner.ResourceCount; i++)
        {
            Assert.AreEqual(1, replacement.Owner.GetResourceRecord(i).OwnerReferenceCount);
        }

        Assert.IsTrue(active.Owner.GetResourceRecord(0).TryPromoteRetiredReady(isRetirementFenceCompleted: true));
    }

    [TestMethod]
    public void ReleasesOwnerReferenceOnTrim()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));
        Assert.IsTrue(slot.TryTrim());

        AssertAllOwnerReferencesReleased(active);
    }

    [TestMethod]
    public void ReleasesOwnerReferenceOnDisposeOnlyOnce()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        _ = slot.RequestDispose();
        _ = slot.RequestDispose();

        AssertAllOwnerReferencesReleased(active);
    }

    [TestMethod]
    public void ReleasesOwnerReferenceWhenRetiredSetIsCleared()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle retired = Handle(1, 1);

        Assert.IsTrue(slot.TryInstallPrepared(retired, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        ResourceGenerationSetHandle active = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 2));
        Assert.IsTrue(slot.TryCommitReplacement(retired.SetId, slot.BindingEpoch, 2, out _));

        _ = slot.RequestDispose();

        Assert.AreEqual(1, active.Owner.GetResourceRecord(0).OwnerReferenceCount);

        ReleaseAll(retired);

        Assert.IsTrue(slot.TryClearRetired(retired.SetId));

        AssertAllOwnerReferencesReleased(active);
    }

    [TestMethod]
    public void RejectsRetirementOfGenerationWithoutOwnerReference()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        active.Owner.GetResourceRecord(1).ReleaseOwnerReference();

        _ = Assert.ThrowsException<InvalidOperationException>(() => slot.TryTrim());

        Assert.AreEqual(ResourceGenerationState.Active, active.Owner.GetResourceRecord(0).Lifecycle);
        Assert.AreEqual(1, active.Owner.GetResourceRecord(0).OwnerReferenceCount);
        Assert.AreEqual(SlotControlState.Active, slot.State);
    }

    [TestMethod]
    public void RetiresEveryMemberOfReplacedGeneration()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 3);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        ResourceGenerationSetHandle replacement = Handle(2, 2, resourceCount: 3);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));

        AssertAllRetireRequested(active);

        for (int i = 0; i < replacement.Owner.ResourceCount; i++)
        {
            Assert.AreEqual(ResourceGenerationState.Active, replacement.Owner.GetResourceRecord(i).Lifecycle);
        }
    }

    [TestMethod]
    public void RetiresEveryMemberOnTrim()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));
        Assert.IsTrue(slot.TryTrim());

        AssertAllRetireRequested(active);
    }

    [TestMethod]
    public void RetiresEveryMemberOnDisposeWithoutRetiredGeneration()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle active = Handle(1, 1, resourceCount: 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        _ = slot.RequestDispose();

        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);

        AssertAllRetireRequested(active);

        _ = slot.RequestDispose();

        AssertAllRetireRequested(active);
    }

    [TestMethod]
    public void RetiresActiveGenerationWhenRetiredSetIsCleared()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());

        ResourceGenerationSetHandle retired = Handle(1, 1);

        Assert.IsTrue(slot.TryInstallPrepared(retired, 1));
        Assert.IsTrue(slot.TryCommitReplacement(default, 0, 1, out _));

        ResourceGenerationSetHandle active = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(active, 2));
        Assert.IsTrue(slot.TryCommitReplacement(retired.SetId, slot.BindingEpoch, 2, out _));

        _ = slot.RequestDispose();

        Assert.AreEqual(SlotControlState.DisposeWaitingForRetired, slot.State);
        Assert.AreEqual(ResourceGenerationState.Active, active.Owner.GetResourceRecord(0).Lifecycle);

        ReleaseAll(retired);

        Assert.IsTrue(slot.TryClearRetired(retired.SetId));
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);

        AssertAllRetireRequested(active);
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
    public void RejectsEmptyPreparedHandle()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());
        Assert.IsFalse(slot.TryInstallPrepared(default, 1));
        Assert.AreEqual(SlotControlState.Active, slot.State);
    }

    [TestMethod]
    public void PinsOnlyMatchingGenerationAndEpoch()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0, out _));
        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(2), slot.BindingEpoch, 0, out _));
        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch + 1, 0, out _));
        Assert.IsFalse(slot.TryPin(new ResourceGenerationSetId(9), new ResourceGenerationId(1), slot.BindingEpoch, 0, out _));
    }

    [TestMethod]
    public void RejectsPinWithResourceIndexOutOfRange()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 1, out _));
        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, -1, out _));
    }

    [TestMethod]
    public void ReleasesPinOnPinnedGenerationAfterReplacement()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0, out ResourceGenerationPin pin));

        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));

        Assert.AreEqual(1, active.Owner.GetResourceRecord(0).RecordingReferenceCount);
        Assert.AreEqual(0, replacement.Owner.GetResourceRecord(0).RecordingReferenceCount);

        SlotControlRecord.ReleasePin(pin);

        Assert.AreEqual(0, active.Owner.GetResourceRecord(0).RecordingReferenceCount);
        Assert.AreEqual(0, replacement.Owner.GetResourceRecord(0).RecordingReferenceCount);
    }

    [TestMethod]
    public void ReleasesPinOnPinnedGenerationAfterTrim()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0, out ResourceGenerationPin pin));
        Assert.IsTrue(slot.TryTrim());
        Assert.IsTrue(slot.Active.IsEmpty);

        SlotControlRecord.ReleasePin(pin);

        Assert.AreEqual(0, active.Owner.GetResourceRecord(0).RecordingReferenceCount);
    }

    [TestMethod]
    public void RejectsPinAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        _ = slot.RequestDispose();

        Assert.IsFalse(slot.TryPin(active.SetId, new ResourceGenerationId(1), slot.BindingEpoch, 0, out _));
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
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out ResourceGenerationSetHandle detached));
        Assert.IsTrue(detached.IsEmpty);
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.AreEqual(replacement.SetId, slot.Active.SetId);
        Assert.AreEqual(active.SetId, slot.Retired.SetId);
        Assert.AreEqual(2ul, slot.BindingEpoch);
        Assert.AreEqual(0ul, slot.PreparedToken);
    }

    [TestMethod]
    public void ReturnsDetachedHandleOnStaleCommit()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsFalse(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch + 1, 2, out ResourceGenerationSetHandle detached));
        Assert.AreEqual(replacement.SetId, detached.SetId);
        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(SlotControlState.Active, slot.State);
        Assert.AreEqual(active.SetId, slot.Active.SetId);
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void ReturnsDetachedHandleOnAbort()
    {
        SlotControlRecord slot = BoundSlot(out _);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryAbortReplacement(2, out ResourceGenerationSetHandle detached));
        Assert.AreEqual(replacement.SetId, detached.SetId);
        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void KeepsPreparedOnStaleAbortToken()
    {
        SlotControlRecord slot = BoundSlot(out _);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsFalse(slot.TryAbortReplacement(99, out ResourceGenerationSetHandle detached));
        Assert.IsTrue(detached.IsEmpty);
        Assert.AreEqual(replacement.SetId, slot.Prepared.SetId);
    }

    [TestMethod]
    public void SkipsTrimWhenRetiredPresent()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));
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

        Assert.IsTrue(slot.RequestDispose().IsEmpty);
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void DisposesSlotWithoutActiveDirectly()
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());
        Assert.IsTrue(slot.RequestDispose().IsEmpty);
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void ReturnsPreparedHandleOnDispose()
    {
        SlotControlRecord slot = BoundSlot(out _);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));

        ResourceGenerationSetHandle detached = slot.RequestDispose();

        Assert.AreEqual(replacement.SetId, detached.SetId);
        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(0ul, slot.PreparedToken);
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
    }

    [TestMethod]
    public void BlocksDisposeUntilActiveReleased()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        _ = slot.RequestDispose();

        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.IsFalse(slot.TryCompleteRetiringActive());

        ReleaseAll(active);

        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
        Assert.AreEqual(2ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void BlocksRetiredClearUntilAllMembersReleased()
    {
        ResourceGenerationSetHandle active = Handle(1, 1, 2);

        SlotControlRecord multi = default;

        Assert.IsTrue(multi.TryBind());
        Assert.IsTrue(multi.TryInstallPrepared(active, 1));
        Assert.IsTrue(multi.TryCommitReplacement(default, 0, 1, out _));
        Assert.IsTrue(multi.TryTrim());

        ref ResourceGenerationRecord first = ref active.Owner.GetResourceRecord(0);

        first.Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(first.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(first.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsFalse(multi.TryClearRetired(active.SetId));

        ReleaseAll(active);

        Assert.IsTrue(multi.TryClearRetired(active.SetId));
        Assert.IsTrue(multi.Retired.IsEmpty);
    }

    [TestMethod]
    public void RejectsRetiredClearWithMismatchedSetId()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryTrim());

        ReleaseAll(active);

        Assert.IsFalse(slot.TryClearRetired(new ResourceGenerationSetId(9)));
        Assert.IsTrue(slot.TryClearRetired(active.SetId));
    }

    [TestMethod]
    public void WaitsForRetiredBeforeRetiringActive()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));

        _ = slot.RequestDispose();

        Assert.AreEqual(SlotControlState.DisposeWaitingForRetired, slot.State);
        Assert.IsFalse(slot.TryCompleteRetiringActive());

        ReleaseAll(active);

        Assert.IsTrue(slot.TryClearRetired(active.SetId));
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.IsFalse(slot.TryCompleteRetiringActive());

        ReleaseAll(replacement);

        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
    }

    [TestMethod]
    public void RejectsCommitAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryInstallPrepared(Handle(2, 2), 2));

        ulong epoch = slot.BindingEpoch;

        _ = slot.RequestDispose();

        Assert.IsFalse(slot.TryCommitReplacement(active.SetId, epoch, 2, out _));
        Assert.AreEqual(active.SetId, slot.Active.SetId);
    }

    [TestMethod]
    public void RejectsPreparedInstallAfterDisposeRequested()
    {
        SlotControlRecord slot = BoundSlot(out _);

        _ = slot.RequestDispose();

        Assert.IsFalse(slot.TryInstallPrepared(Handle(2, 2), 2));
    }

    [TestMethod]
    public void RejectsPreparedInstallWhileRetiredPresent()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));
        Assert.IsFalse(slot.Retired.IsEmpty);
        Assert.IsFalse(slot.TryInstallPrepared(Handle(3, 3), 3));
        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(SlotControlState.Active, slot.State);
    }

    [TestMethod]
    public void RetainsPreparedGenerationOnDeviceTerminal()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle prepared = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(prepared, 2));
        Assert.IsTrue(slot.TryMarkDeviceTerminal());
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);
        Assert.AreEqual(0ul, slot.PreparedToken);

        Assert.AreEqual(active.SetId, slot.Active.SetId);
        Assert.AreEqual(prepared.SetId, slot.Prepared.SetId);

        Assert.AreEqual(ResourceGenerationState.TerminalRetained, active.Owner.GetResourceRecord(0).Lifecycle);
        Assert.AreEqual(ResourceGenerationState.TerminalRetained, prepared.Owner.GetResourceRecord(0).Lifecycle);

        Assert.IsFalse(slot.TryCompleteRetiringActive());
    }

    [TestMethod]
    public void RetainsRetiredGenerationOnDeviceTerminal()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);
        ResourceGenerationSetHandle replacement = Handle(2, 2);

        Assert.IsTrue(slot.TryInstallPrepared(replacement, 2));
        Assert.IsTrue(slot.TryCommitReplacement(active.SetId, slot.BindingEpoch, 2, out _));
        Assert.IsTrue(slot.TryMarkDeviceTerminal());
        Assert.AreEqual(SlotControlState.RetiringActive, slot.State);

        Assert.AreEqual(replacement.SetId, slot.Active.SetId);
        Assert.AreEqual(active.SetId, slot.Retired.SetId);

        Assert.AreEqual(ResourceGenerationState.TerminalRetained, active.Owner.GetResourceRecord(0).Lifecycle);
        Assert.AreEqual(ResourceGenerationState.TerminalRetained, replacement.Owner.GetResourceRecord(0).Lifecycle);

        Assert.IsFalse(slot.TryCompleteRetiringActive());
    }

    [TestMethod]
    public void CompletesDisposeAfterDeviceTeardownRelease()
    {
        SlotControlRecord slot = BoundSlot(out ResourceGenerationSetHandle active);

        Assert.IsTrue(slot.TryMarkDeviceTerminal());
        Assert.IsFalse(slot.TryCompleteRetiringActive());

        ref ResourceGenerationRecord record = ref active.Owner.GetResourceRecord(0);

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.DeviceTeardown));
        Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.DeviceTeardown));

        Assert.IsTrue(slot.TryCompleteRetiringActive());
        Assert.AreEqual(SlotControlState.Disposed, slot.State);
        Assert.AreEqual(2ul, slot.BindingEpoch);
        Assert.IsFalse(slot.TryMarkDeviceTerminal());
    }
}
