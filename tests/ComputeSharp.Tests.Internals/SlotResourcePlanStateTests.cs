using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class SlotResourcePlanStateTests
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

    private static ResourcePlanFieldDescriptor Field(uint fieldOrdinal, uint slotResourceIndex, ResourcePlanDimensionKind dimensionKind)
    {
        return new ResourcePlanFieldDescriptor(
            fieldOrdinal,
            slotResourceIndex,
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            "memberLength",
            dimensionKind);
    }

    private static OwnedSlotDescriptor Slot(uint ordinal, ResourcePlanKind planKind, params ResourcePlanFieldDescriptor[] planFields)
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(ordinal),
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            planKind is ResourcePlanKind.ResourceGroup ? ResourceOwnershipKind.OwnedGroupSlot : ResourceOwnershipKind.OwnedSlot,
            planKind,
            ComputeResourceRecovery.Discardable,
            planFields);
    }

    private static OwnedSlotDescriptor BufferSlot()
    {
        return Slot(0, ResourcePlanKind.Buffer, Field(0, 0, ResourcePlanDimensionKind.Length));
    }

    private static OwnedSlotDescriptor GroupSlot()
    {
        return Slot(
            1,
            ResourcePlanKind.ResourceGroup,
            Field(0, 0, ResourcePlanDimensionKind.Length),
            Field(1, 1, ResourcePlanDimensionKind.Width),
            Field(2, 1, ResourcePlanDimensionKind.Height));
    }

    private static PipelineHostDescriptor Host(params OwnedSlotDescriptor[] slots)
    {
        return new PipelineHostDescriptor(
            new PipelineSchemaVersion(1, 0, 1),
            default,
            "Ukiyoe.Host",
            1,
            new StaticStructuralRequirements(1, 1, slots.Length),
            default,
            slots);
    }

    private static SharedTextureContractDescriptor SharedTexture(uint ordinal, ComputeResourceResizePolicy resizePolicy)
    {
        return new SharedTextureContractDescriptor(
            new SlotOrdinal(ordinal),
            "Source",
            "ComputeSharp.Interop.ComputeSharedTexture2D`1",
            resizePolicy,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost);
    }

    private static InteropResourceSetDescriptor ResourceSet(params SharedTextureContractDescriptor[] sharedTextures)
    {
        return new InteropResourceSetDescriptor(
            new PipelineSchemaVersion(1, 0, 1),
            default,
            "Ukiyoe.ResourceSet",
            new ResourceSetStructuralRequirements(sharedTextures.Length),
            sharedTextures);
    }

    private static SlotControlRecord PublishedSlot(
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ResourceGenerationSetHandle active,
        params int[] plan)
    {
        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());
        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, active, 1, plan));
        Assert.IsTrue(SlotResourcePlanController.TryCommitReplacement(ref slot, storage, planState, default, 0, 1, out _));

        return slot;
    }

    private static SlotControlRecord SlotWithRetiredGeneration(
        int[] storage,
        SlotResourcePlanStateRecord planState,
        ResourceGenerationSetHandle retired,
        ResourceGenerationSetHandle active,
        int[] retiredPlan,
        int[] activePlan)
    {
        SlotControlRecord slot = PublishedSlot(storage, planState, retired, retiredPlan);

        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, active, 2, activePlan));
        Assert.IsTrue(SlotResourcePlanController.TryCommitReplacement(ref slot, storage, planState, retired.SetId, slot.BindingEpoch, 2, out _));
        Assert.AreEqual(retired.SetId, slot.Retired.SetId);

        return slot;
    }

    [TestMethod]
    public void ReservesContiguousHostPlanStorage()
    {
        SlotResourcePlanStateRecord[] states = new SlotResourcePlanStateRecord[2];

        int capacity = SlotResourcePlanStorage.CreateHostPlanStates(Host(BufferSlot(), GroupSlot()), states);

        Assert.AreEqual(12, capacity);
        Assert.AreEqual(0, states[0].StorageOffset);
        Assert.AreEqual(1, states[0].FieldCount);
        Assert.AreEqual(3, states[1].StorageOffset);
        Assert.AreEqual(3, states[1].FieldCount);
    }

    [TestMethod]
    public void ReservesContiguousResourceSetPlanStorage()
    {
        SlotResourcePlanStateRecord[] states = new SlotResourcePlanStateRecord[2];

        int capacity = SlotResourcePlanStorage.CreateResourceSetPlanStates(
            ResourceSet(
                SharedTexture(0, ComputeResourceResizePolicy.Exact),
                SharedTexture(1, ComputeResourceResizePolicy.GrowOnly)),
            states);

        Assert.AreEqual(12, capacity);
        Assert.AreEqual(0, states[0].StorageOffset);
        Assert.AreEqual(2, states[0].FieldCount);
        Assert.AreEqual(6, states[1].StorageOffset);
        Assert.AreEqual(2, states[1].FieldCount);
    }

    [TestMethod]
    public void SeparatesPlanRegionsWithinOneSlot()
    {
        SlotResourcePlanStateRecord planState = new(4, 3);
        int[] storage = new int[16];

        SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState).Fill(1);
        SlotResourcePlanStorage.GetActivePhysicalCapacity(storage, planState).Fill(2);
        SlotResourcePlanStorage.GetPreparedPlan(storage, planState).Fill(3);

        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 0, 0, 0 }, storage);

        SlotResourcePlanStorage.ClearSlot(storage, planState);

        CollectionAssert.AreEqual(new int[16], storage);
    }

    [TestMethod]
    public void PublishesPreparedPlanToLogicalAndCapacityOnCommit()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        _ = PublishedSlot(storage, planState, Handle(1, 1), 1024);

        CollectionAssert.AreEqual(new[] { 1024, 1024, 0 }, storage);
    }

    [TestMethod]
    public void KeepsActivePlanWhenCommitEpochIsStale()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 1024);

        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, Handle(2, 2), 2, [2048]));
        Assert.IsFalse(SlotResourcePlanController.TryCommitReplacement(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch + 1, 2, out ResourceGenerationSetHandle detached));
        Assert.IsFalse(detached.IsEmpty);

        CollectionAssert.AreEqual(new[] { 1024, 1024, 0 }, storage);
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void DoesNotLeakPreparedPlanIntoActiveOnAbort()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 1024);

        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, Handle(2, 2), 2, [2048]));

        CollectionAssert.AreEqual(new[] { 1024, 1024, 2048 }, storage);

        Assert.IsTrue(SlotResourcePlanController.TryAbortReplacement(ref slot, storage, planState, 2, out ResourceGenerationSetHandle detached));
        Assert.IsFalse(detached.IsEmpty);

        CollectionAssert.AreEqual(new[] { 1024, 1024, 0 }, storage);
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void ClearsPreparedPlanWhenDisposeDetachesPrepared()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 1024);

        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, Handle(2, 2), 2, [2048]));
        Assert.IsFalse(SlotResourcePlanController.RequestDispose(ref slot, storage, planState).IsEmpty);

        CollectionAssert.AreEqual(new[] { 1024, 1024, 0 }, storage);
    }

    [TestMethod]
    public void ClearsActivePlanOnTrim()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 1024);

        Assert.IsTrue(SlotResourcePlanController.TryTrim(ref slot, storage, planState));

        CollectionAssert.AreEqual(new int[3], storage);
        Assert.AreEqual(2ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void ClearsPlanStorageWhenSlotReachesDisposed()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        ResourceGenerationSetHandle active = Handle(1, 1);
        SlotControlRecord slot = PublishedSlot(storage, planState, active, 1024);

        _ = SlotResourcePlanController.RequestDispose(ref slot, storage, planState);

        ref ResourceGenerationRecord record = ref active.Owner.GetResourceRecord(0);

        record.Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(SlotResourcePlanController.TryCompleteRetiringActive(ref slot, storage, planState));

        CollectionAssert.AreEqual(new int[3], storage);
    }

    [TestMethod]
    public void AppliesLogicalUpdateWithoutChangingCapacityOrEpoch()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);

        Assert.IsTrue(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [320, 240]));

        CollectionAssert.AreEqual(new[] { 320, 240, 640, 480, 0, 0 }, storage);
        Assert.AreEqual(1ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void RejectsLogicalUpdateWhileReplacementPrepared()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);

        Assert.IsTrue(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, Handle(2, 2), 2, [800, 600]));
        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [320, 240]));

        CollectionAssert.AreEqual(new[] { 640, 480, 640, 480, 800, 600 }, storage);
    }

    [TestMethod]
    public void RejectsLogicalUpdateAfterDisposeRequested()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);

        _ = SlotResourcePlanController.RequestDispose(ref slot, storage, planState);

        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [320, 240]));

        CollectionAssert.AreEqual(new[] { 640, 480, 640, 480, 0, 0 }, storage);
    }

    [TestMethod]
    public void RejectsLogicalUpdateAfterTrim()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        ResourceGenerationSetHandle active = Handle(1, 1);
        SlotControlRecord slot = PublishedSlot(storage, planState, active, 640, 480);

        Assert.IsTrue(SlotResourcePlanController.TryTrim(ref slot, storage, planState));
        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, active.SetId, slot.BindingEpoch, [320, 240]));

        CollectionAssert.AreEqual(new int[6], storage);
    }

    [TestMethod]
    public void RejectsLogicalUpdateWithStaleEpoch()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);

        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch + 1, [320, 240]));

        CollectionAssert.AreEqual(new[] { 640, 480, 640, 480, 0, 0 }, storage);
    }

    [TestMethod]
    public void RejectsLogicalUpdateBeyondPhysicalCapacity()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);

        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [641, 480]));
        Assert.IsFalse(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [640, 481]));

        CollectionAssert.AreEqual(new[] { 640, 480, 640, 480, 0, 0 }, storage);
    }

    [TestMethod]
    public void AppliesLogicalUpdateWhileRetiredGenerationIsPending()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        ResourceGenerationSetHandle retired = Handle(1, 1);
        ResourceGenerationSetHandle active = Handle(2, 2);
        SlotControlRecord slot = SlotWithRetiredGeneration(storage, planState, retired, active, [640, 480], [800, 600]);

        Assert.IsTrue(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, active.SetId, slot.BindingEpoch, [640, 480]));

        CollectionAssert.AreEqual(new[] { 640, 480, 800, 600, 0, 0 }, storage);
        Assert.AreEqual(active.SetId, slot.Active.SetId);
        Assert.AreEqual(retired.SetId, slot.Retired.SetId);
        Assert.AreEqual(2ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void ReportsIdenticalWhileRetiredGenerationIsPending()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        ResourceGenerationSetHandle retired = Handle(1, 1);
        ResourceGenerationSetHandle active = Handle(2, 2);
        SlotControlRecord slot = SlotWithRetiredGeneration(storage, planState, retired, active, [640, 480], [800, 600]);

        Assert.AreEqual(
            ResourcePlanDecision.Identical,
            SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, SharedTexture(0, ComputeResourceResizePolicy.GrowOnly), 800, 600));
        Assert.AreEqual(retired.SetId, slot.Retired.SetId);
    }

    [TestMethod]
    public void RejectsReplacementWhileRetiredGenerationIsPending()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        ResourceGenerationSetHandle retired = Handle(1, 1);
        ResourceGenerationSetHandle active = Handle(2, 2);
        SlotControlRecord slot = SlotWithRetiredGeneration(storage, planState, retired, active, [640, 480], [800, 600]);

        Assert.AreEqual(
            ResourcePlanDecision.Replacement,
            SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, SharedTexture(0, ComputeResourceResizePolicy.GrowOnly), 1024, 768));
        Assert.IsFalse(SlotResourcePlanController.TryInstallPrepared(ref slot, storage, planState, Handle(3, 3), 3, [1024, 768]));

        Assert.IsTrue(slot.Prepared.IsEmpty);
        Assert.AreEqual(0ul, slot.PreparedToken);
        CollectionAssert.AreEqual(new[] { 800, 600, 800, 600, 0, 0 }, storage);
        Assert.AreEqual(2ul, slot.BindingEpoch);
    }

    [TestMethod]
    public void ReportsReplacementWhenActiveIsEmpty()
    {
        SlotResourcePlanStateRecord planState = new(0, 1);
        int[] storage = new int[3];

        SlotControlRecord slot = default;

        Assert.IsTrue(slot.TryBind());
        Assert.AreEqual(
            ResourcePlanDecision.Replacement,
            SlotResourcePlanController.Evaluate(slot, storage, planState, BufferSlot(), [1024]));
    }

    [TestMethod]
    public void ReplacesWholeGroupWhenAnyMemberDiffersFromActivePlan()
    {
        SlotResourcePlanStateRecord planState = new(0, 3);
        int[] storage = new int[9];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 16, 8, 4);
        OwnedSlotDescriptor descriptor = GroupSlot();

        Assert.AreEqual(ResourcePlanDecision.Identical, SlotResourcePlanController.Evaluate(slot, storage, planState, descriptor, [16, 8, 4]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, SlotResourcePlanController.Evaluate(slot, storage, planState, descriptor, [16, 8, 5]));
    }

    [TestMethod]
    public void ReusesGrowOnlySharedTextureAfterLogicalShrink()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);
        SharedTextureContractDescriptor descriptor = SharedTexture(0, ComputeResourceResizePolicy.GrowOnly);

        Assert.IsTrue(SlotResourcePlanController.TryApplyLogicalUpdate(ref slot, storage, planState, slot.Active.SetId, slot.BindingEpoch, [320, 240]));

        Assert.AreEqual(ResourcePlanDecision.Identical, SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, descriptor, 320, 240));
        Assert.AreEqual(ResourcePlanDecision.LogicalUpdate, SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, descriptor, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Replacement, SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, descriptor, 641, 480));
    }

    [TestMethod]
    public void ReplacesExactSharedTextureAfterAnyDimensionChange()
    {
        SlotResourcePlanStateRecord planState = new(0, 2);
        int[] storage = new int[6];

        SlotControlRecord slot = PublishedSlot(storage, planState, Handle(1, 1), 640, 480);
        SharedTextureContractDescriptor descriptor = SharedTexture(0, ComputeResourceResizePolicy.Exact);

        Assert.AreEqual(ResourcePlanDecision.Identical, SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, descriptor, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Replacement, SlotResourcePlanController.EvaluateSharedTexture(slot, storage, planState, descriptor, 320, 240));
    }
}
