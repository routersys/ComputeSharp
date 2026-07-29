using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe class ResourceHazardTrackerTests
{
    private sealed class GenerationOwner(ulong setId, int resourceCount) : IResourceGenerationOwner
    {
        private readonly ResourceGenerationRecord[] records = new ResourceGenerationRecord[resourceCount];

        public ResourceGenerationSetId SetId { get; } = new(setId);

        public int ResourceCount => this.records.Length;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.records[resourceOrdinal];
        }

        public ID3D12Resource* GetResourceNativePointer(int resourceOrdinal)
        {
            return null;
        }
    }

    private static ResourceGenerationSetHandle Handle(ulong setId = 1, int resourceCount = 2)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(setId, resourceCount));
    }

    private static GraphicsResourceUsageEntry Usage(
        ResourceGenerationSetHandle set,
        uint resourceIndex,
        ComputeResourceAccess access,
        TrackedResourceState firstState,
        TrackedResourceState finalState)
    {
        return new GraphicsResourceUsageEntry
        {
            Set = set,
            ResourceIndex = resourceIndex,
            Generation = new ResourceGenerationId(resourceIndex + 1),
            Access = access,
            FirstState = firstState,
            FinalState = finalState
        };
    }

    [TestMethod]
    public void PlansATransitionWhenTheRecordedStateDiffers()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.Common;

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        ResourceBarrierPlanEntry[] prologue = new ResourceBarrierPlanEntry[1];

        int count = ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out ulong wait);

        Assert.AreEqual(1, count);
        Assert.AreEqual(0ul, wait);
        Assert.AreEqual(ResourceBarrierKind.Transition, prologue[0].Kind);
        Assert.AreEqual(TrackedResourceState.Common, prologue[0].BeforeState);
        Assert.AreEqual(TrackedResourceState.UnorderedAccess, prologue[0].AfterState);
    }

    [TestMethod]
    public void PlansAnUnorderedAccessBarrierBetweenSuccessiveWrites()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Compute, 4);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        ResourceBarrierPlanEntry[] prologue = new ResourceBarrierPlanEntry[1];

        int count = ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out _);

        Assert.AreEqual(1, count);
        Assert.AreEqual(ResourceBarrierKind.UnorderedAccess, prologue[0].Kind);
    }

    [TestMethod]
    public void PlansNoBarrierWithoutAPreviousWrite()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        ResourceBarrierPlanEntry[] prologue = new ResourceBarrierPlanEntry[1];

        Assert.AreEqual(0, ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out _));
    }

    [TestMethod]
    public void WaitsOnAWriteIssuedFromAnotherQueue()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Copy, 9);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        ResourceBarrierPlanEntry[] prologue = new ResourceBarrierPlanEntry[1];

        int count = ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out ulong wait);

        Assert.AreEqual(9ul, wait);
        Assert.AreEqual(1, count);
        Assert.AreEqual(ResourceBarrierKind.UnorderedAccess, prologue[0].Kind);
    }

    [TestMethod]
    public void WaitsOnForeignReadsOnlyForWritingAccess()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(0).LastCopyRead = new FencePoint(ComputeQueueKind.Copy, 6);

        GraphicsResourceUsageEntry[] reading =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        _ = ResourceHazardTracker.PlanQueueDependencies(reading, ComputeQueueKind.Compute, new ResourceBarrierPlanEntry[1], out ulong readWait);

        Assert.AreEqual(0ul, readWait);

        GraphicsResourceUsageEntry[] writing =
        [
            Usage(set, 0, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        _ = ResourceHazardTracker.PlanQueueDependencies(writing, ComputeQueueKind.Compute, new ResourceBarrierPlanEntry[1], out ulong writeWait);

        Assert.AreEqual(6ul, writeWait);
    }

    [TestMethod]
    public void TakesTheHighestWaitValueAcrossEveryUsage()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Copy, 3);
        set.Owner.GetResourceRecord(1).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(1).LastWrite = new FencePoint(ComputeQueueKind.Copy, 11);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess),
            Usage(set, 1, ComputeResourceAccess.Read, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        _ = ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, new ResourceBarrierPlanEntry[2], out ulong wait);

        Assert.AreEqual(11ul, wait);
    }

    [TestMethod]
    public void PlansManualCopyHandoffAndQueueDependencies()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.UnorderedAccess;
        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Compute, 9);
        set.Owner.GetResourceRecord(0).LastCopyRead = new FencePoint(ComputeQueueKind.Copy, 4);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Write, TrackedResourceState.Common, TrackedResourceState.Common)
        ];

        ResourceBarrierPlanEntry[] handoff = new ResourceBarrierPlanEntry[1];

        int count = ResourceHazardTracker.PlanManualCopyDependencies(
            usages,
            handoff,
            out ulong handoffWait,
            out ulong copyWait);

        Assert.AreEqual(1, count);
        Assert.AreEqual(4ul, handoffWait);
        Assert.AreEqual(9ul, copyWait);
        Assert.AreEqual(ResourceBarrierKind.Transition, handoff[0].Kind);
        Assert.AreEqual(TrackedResourceState.UnorderedAccess, handoff[0].BeforeState);
        Assert.AreEqual(TrackedResourceState.Common, handoff[0].AfterState);
    }

    [TestMethod]
    public void PlansManualCopyWithoutHandoffFromCommon()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.Common;
        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Compute, 7);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.Common, TrackedResourceState.Common)
        ];

        int count = ResourceHazardTracker.PlanManualCopyDependencies(
            usages,
            new ResourceBarrierPlanEntry[1],
            out ulong handoffWait,
            out ulong copyWait);

        Assert.AreEqual(0, count);
        Assert.AreEqual(0ul, handoffWait);
        Assert.AreEqual(7ul, copyWait);
    }

    [TestMethod]
    public void CommitsReadsWithoutClearingTheLastWrite()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).LastWrite = new FencePoint(ComputeQueueKind.Compute, 2);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.Common, TrackedResourceState.NonPixelShaderResource)
        ];

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 7), 11);

        ref ResourceGenerationRecord record = ref set.Owner.GetResourceRecord(0);

        Assert.AreEqual(7ul, record.LastComputeRead.Value);
        Assert.AreEqual(2ul, record.LastWrite.Value);
        Assert.IsTrue(record.LastCopyRead.IsNone);
        Assert.AreEqual(TrackedResourceState.NonPixelShaderResource, record.D3D12State);
    }

    [TestMethod]
    public void CommitsWritesByClearingEveryRecordedRead()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).LastComputeRead = new FencePoint(ComputeQueueKind.Compute, 2);
        set.Owner.GetResourceRecord(0).LastCopyRead = new FencePoint(ComputeQueueKind.Copy, 3);

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess)
        ];

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 8), 12);

        ref ResourceGenerationRecord record = ref set.Owner.GetResourceRecord(0);

        Assert.AreEqual(8ul, record.LastWrite.Value);
        Assert.IsTrue(record.LastComputeRead.IsNone);
        Assert.IsTrue(record.LastCopyRead.IsNone);
        Assert.AreEqual(TrackedResourceState.UnorderedAccess, record.D3D12State);
    }

    [TestMethod]
    public void PlansNoBarrierAfterCommittingTheSameState()
    {
        ResourceGenerationSetHandle set = Handle();

        set.Owner.GetResourceRecord(0).D3D12State = TrackedResourceState.Common;

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.NonPixelShaderResource, TrackedResourceState.NonPixelShaderResource)
        ];

        ResourceBarrierPlanEntry[] prologue = new ResourceBarrierPlanEntry[1];

        Assert.AreEqual(1, ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out _));

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 1), 13);

        Assert.AreEqual(0, ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, prologue, out _));
    }

    [TestMethod]
    public void CommitsTheLogicalSubmissionSequenceOfEveryUsedGeneration()
    {
        ResourceGenerationSetHandle set = Handle();

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.Common, TrackedResourceState.Common)
        ];

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 4), 9);

        Assert.AreEqual(9ul, set.Owner.GetResourceRecord(0).LastUseSequence);

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 5), 12);

        Assert.AreEqual(12ul, set.Owner.GetResourceRecord(0).LastUseSequence);

        ResourceHazardTracker.CommitResourceUsages(usages, new FencePoint(ComputeQueueKind.Compute, 6), 3);

        Assert.AreEqual(12ul, set.Owner.GetResourceRecord(0).LastUseSequence);
    }

    [TestMethod]
    public void RejectsUsagesWithoutRecordedStates()
    {
        ResourceGenerationSetHandle set = Handle();

        GraphicsResourceUsageEntry[] usages =
        [
            Usage(set, 0, ComputeResourceAccess.Read, TrackedResourceState.Unknown, TrackedResourceState.Common)
        ];

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, new ResourceBarrierPlanEntry[1], out _));

        _ = Assert.ThrowsExactly<ArgumentException>(
            () => ResourceHazardTracker.PlanQueueDependencies(usages, ComputeQueueKind.Compute, [], out _));

        _ = Assert.ThrowsExactly<ArgumentException>(
            () => ResourceHazardTracker.CommitResourceUsages(usages, FencePoint.None, 0));
    }
}
