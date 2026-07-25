using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceUsageTrackerTests
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
    }

    private static ResourceGenerationSetHandle Handle(ulong setId, int resourceCount = 2)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(setId, resourceCount));
    }

    private static UsageSetPoolEntry Set(int storageOffset, int capacity)
    {
        return new UsageSetPoolEntry { StorageOffset = storageOffset, Capacity = capacity, Count = 0 };
    }

    [TestMethod]
    public void UnionsAccessOnTheDeclaredLattice()
    {
        Assert.AreEqual(ComputeResourceAccess.Read, ResourceUsageTracker.Union(ComputeResourceAccess.Read, ComputeResourceAccess.Read));
        Assert.AreEqual(ComputeResourceAccess.Write, ResourceUsageTracker.Union(ComputeResourceAccess.Write, ComputeResourceAccess.Write));
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, ResourceUsageTracker.Union(ComputeResourceAccess.Read, ComputeResourceAccess.Write));
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, ResourceUsageTracker.Union(ComputeResourceAccess.Write, ComputeResourceAccess.Read));
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, ResourceUsageTracker.Union(ComputeResourceAccess.ReadWrite, ComputeResourceAccess.Read));
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, ResourceUsageTracker.Union(ComputeResourceAccess.ReadWrite, ComputeResourceAccess.ReadWrite));
    }

    [TestMethod]
    public void AcceptsOnlyObservedAccessWithinDeclaredContract()
    {
        Assert.IsTrue(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Read, ComputeResourceAccess.Read));
        Assert.IsFalse(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Write, ComputeResourceAccess.Read));
        Assert.IsFalse(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.ReadWrite, ComputeResourceAccess.Read));

        Assert.IsTrue(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Write, ComputeResourceAccess.Write));
        Assert.IsFalse(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Read, ComputeResourceAccess.Write));
        Assert.IsFalse(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.ReadWrite, ComputeResourceAccess.Write));

        Assert.IsTrue(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Read, ComputeResourceAccess.ReadWrite));
        Assert.IsTrue(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.Write, ComputeResourceAccess.ReadWrite));
        Assert.IsTrue(ResourceUsageTracker.IsWithinDeclared(ComputeResourceAccess.ReadWrite, ComputeResourceAccess.ReadWrite));
    }

    [TestMethod]
    public void AllowsAliasingOnlyWhenEveryContractAllowsIt()
    {
        Assert.IsTrue(ResourceUsageTracker.IsAliasingAllowed(ComputeResourceAliasing.Allow, ComputeResourceAliasing.Allow));
        Assert.IsFalse(ResourceUsageTracker.IsAliasingAllowed(ComputeResourceAliasing.Allow, ComputeResourceAliasing.Disallow));
        Assert.IsFalse(ResourceUsageTracker.IsAliasingAllowed(ComputeResourceAliasing.Disallow, ComputeResourceAliasing.Allow));
        Assert.IsFalse(ResourceUsageTracker.IsAliasingAllowed(ComputeResourceAliasing.Disallow, ComputeResourceAliasing.Disallow));
    }

    [TestMethod]
    public void AppendsEveryDistinctGeneration()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[4];
        UsageSetPoolEntry usageSet = Set(0, 4);
        ResourceGenerationSetHandle set = Handle(1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, out int first, out bool isFirstAliased));
        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 1, new ResourceGenerationId(2), ComputeResourceAccess.Write, out int second, out bool isSecondAliased));

        Assert.AreEqual(0, first);
        Assert.AreEqual(1, second);
        Assert.IsFalse(isFirstAliased);
        Assert.IsFalse(isSecondAliased);
        Assert.AreEqual(2, usageSet.Count);
        Assert.AreEqual(ComputeResourceAccess.Read, storage[0].Access);
        Assert.AreEqual(ComputeResourceAccess.Write, storage[1].Access);
        Assert.AreEqual(TrackedResourceState.Unknown, storage[0].FirstState);
        Assert.AreEqual(TrackedResourceState.Unknown, storage[0].FinalState);
    }

    [TestMethod]
    public void DeduplicatesByGenerationAndUnionsAccess()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[4];
        UsageSetPoolEntry usageSet = Set(0, 4);
        ResourceGenerationSetHandle set = Handle(1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(7), ComputeResourceAccess.Read, out _, out bool isFirstAliased));
        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(7), ComputeResourceAccess.Write, out int index, out bool isSecondAliased));

        Assert.IsFalse(isFirstAliased);
        Assert.IsTrue(isSecondAliased);
        Assert.AreEqual(0, index);
        Assert.AreEqual(1, usageSet.Count);
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, storage[0].Access);
    }

    [TestMethod]
    public void RejectsSameGenerationBoundToDifferentSetSlot()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[4];
        UsageSetPoolEntry usageSet = Set(0, 4);
        ResourceGenerationSetHandle set = Handle(1);
        ResourceGenerationSetHandle other = Handle(2);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(7), ComputeResourceAccess.Read, out _, out _));

        UsageSetPoolEntry capturedSet = usageSet;

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => ResourceUsageTracker.TryAddUsage(storage, ref capturedSet, set, 1, new ResourceGenerationId(7), ComputeResourceAccess.Read, out _, out _));

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => ResourceUsageTracker.TryAddUsage(storage, ref capturedSet, other, 0, new ResourceGenerationId(7), ComputeResourceAccess.Read, out _, out _));
    }

    [TestMethod]
    public void HonoursStorageOffsetOfEveryUsageSet()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[6];
        UsageSetPoolEntry first = Set(0, 3);
        UsageSetPoolEntry second = Set(3, 3);
        ResourceGenerationSetHandle set = Handle(1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref first, set, 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, out _, out _));
        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref second, set, 0, new ResourceGenerationId(1), ComputeResourceAccess.Write, out _, out _));

        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual(ComputeResourceAccess.Read, storage[0].Access);
        Assert.AreEqual(ComputeResourceAccess.Write, storage[3].Access);
    }

    [TestMethod]
    public void RejectsUsageBeyondReservedCapacity()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[2];
        UsageSetPoolEntry usageSet = Set(0, 1);
        ResourceGenerationSetHandle set = Handle(1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, out _, out _));
        Assert.IsFalse(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 1, new ResourceGenerationId(2), ComputeResourceAccess.Read, out int index, out _));

        Assert.AreEqual(-1, index);
        Assert.AreEqual(1, usageSet.Count);
        Assert.AreEqual(default, storage[1].Generation);
    }

    [TestMethod]
    public void RejectsInvalidUsageArguments()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[2];
        UsageSetPoolEntry usageSet = Set(0, 2);
        ResourceGenerationSetHandle set = Handle(1);

        _ = Assert.ThrowsExactly<ArgumentException>(
            () => ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, default, ComputeResourceAccess.Read, out _, out _));

        _ = Assert.ThrowsExactly<ArgumentException>(
            () => ResourceUsageTracker.TryAddUsage(storage, ref usageSet, default, 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, out _, out _));
    }

    [TestMethod]
    public void ClearsEveryTrackedUsage()
    {
        GraphicsResourceUsageEntry[] storage = new GraphicsResourceUsageEntry[2];
        UsageSetPoolEntry usageSet = Set(0, 2);
        ResourceGenerationSetHandle set = Handle(1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, out _, out _));
        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(storage, ref usageSet, set, 1, new ResourceGenerationId(2), ComputeResourceAccess.Read, out _, out _));

        ResourceUsageTracker.ClearUsages(storage, ref usageSet);

        Assert.AreEqual(0, usageSet.Count);
        Assert.AreEqual(default, storage[0].Generation);
        Assert.AreEqual(default, storage[1].Generation);
        Assert.IsTrue(storage[0].Set.IsEmpty);
    }

    [TestMethod]
    public void MapsUsageSetHandleToItsIndex()
    {
        Assert.IsTrue(default(UsageSetHandle).IsNone);
        Assert.AreEqual(1u, UsageSetHandle.FromIndex(0).Value);
        Assert.AreEqual(0, UsageSetHandle.FromIndex(0).ToIndex());
        Assert.AreEqual(41, UsageSetHandle.FromIndex(41).ToIndex());

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => default(UsageSetHandle).ToIndex());
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UsageSetHandle.FromIndex(-1));
    }

    [TestMethod]
    public void TracksUpToThreeCommandListSegments()
    {
        CommandListLeaseSet leaseSet = default;

        Assert.IsTrue(leaseSet.TryAdd(1, 2, ComputeQueueKind.Compute));
        Assert.IsTrue(leaseSet.TryAdd(3, 4, ComputeQueueKind.Compute));
        Assert.IsTrue(leaseSet.TryAdd(5, 6, ComputeQueueKind.Compute));
        Assert.IsFalse(leaseSet.TryAdd(7, 8, ComputeQueueKind.Compute));

        Assert.AreEqual(3, leaseSet.Count);
        Assert.AreEqual(1, CommandListLeaseSet.GetSegment(ref leaseSet, 0).CommandList);
        Assert.AreEqual(4, CommandListLeaseSet.GetSegment(ref leaseSet, 1).CommandAllocator);
        Assert.AreEqual(1, CommandListLeaseSet.GetSegment(ref leaseSet, 2).IsValid);

        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = CommandListLeaseSet.GetSegment(ref leaseSet, 3).IsValid);

        leaseSet.Clear();

        Assert.AreEqual(0, leaseSet.Count);
        Assert.AreEqual(0, leaseSet.Segment0.IsValid);
    }
}
