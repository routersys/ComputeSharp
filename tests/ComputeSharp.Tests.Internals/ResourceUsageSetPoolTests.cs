using System;
using System.Collections.Generic;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceUsageSetPoolTests
{
    private sealed class GenerationOwner(ulong setId) : IResourceGenerationOwner
    {
        private ResourceGenerationRecord record;

        public ResourceGenerationSetId SetId { get; } = new(setId);

        public int ResourceCount => 1;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.record;
        }
    }

    private static ResourceGenerationSetHandle Handle(ulong setId)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(setId));
    }

    private static void CollectHandles(ResourceUsageSetPartition partition, HashSet<uint> handles)
    {
        for (int i = 0; i < partition.SetCount; i++)
        {
            Assert.IsTrue(handles.Add(partition.GetHandle(i).Value), $"Duplicate usage set handle at index {i}.");
        }
    }

    [TestMethod]
    public void ReservesDisjointStorageForEverySetOfAPartition()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition partition = pool.ReservePartition(setCount: 3, entryCapacity: 2);

        Assert.AreEqual(3, partition.SetCount);
        Assert.AreEqual(3, pool.LiveSetCount);
        Assert.AreEqual(6, pool.LiveEntryCount);

        for (int i = 0; i < partition.SetCount; i++)
        {
            ref UsageSetPoolEntry set = ref partition.GetSet(i);

            Assert.AreEqual(2, set.Capacity);
            Assert.AreEqual(0, set.Count);
            Assert.AreEqual(partition.EntryOffset + (i * 2), set.StorageOffset);
        }
    }

    [TestMethod]
    public void AssignsGloballyUniqueHandlesAcrossPartitionsAndSegments()
    {
        ResourceUsageSetPool pool = new();
        HashSet<uint> handles = [];

        ResourceUsageSetPartition first = pool.ReservePartition(4, 2);
        ResourceUsageSetPartition second = pool.ReservePartition(2, 3);
        ResourceUsageSetPartition third = pool.ReservePartition(5, 1);

        CollectHandles(first, handles);
        CollectHandles(second, handles);
        CollectHandles(third, handles);

        Assert.AreEqual(11, handles.Count);
        Assert.AreEqual(11, pool.LiveSetCount);

        for (int i = 0; i < second.SetCount; i++)
        {
            Assert.AreEqual(i, second.GetSetIndex(second.GetHandle(i)));
        }

        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => second.GetSetIndex(first.GetHandle(0)));
    }

    [TestMethod]
    public void KeepsHandlesUniqueAcrossReleaseAndReuseCycles()
    {
        ResourceUsageSetPool pool = new();
        HashSet<uint> handles = [];

        ResourceUsageSetPartition first = pool.ReservePartition(8, 1);
        ResourceUsageSetPartition second = pool.ReservePartition(4, 4);

        Assert.AreEqual(2, pool.SegmentCount);

        pool.ReleasePartition(first);

        ResourceUsageSetPartition third = pool.ReservePartition(3, 2);
        ResourceUsageSetPartition fourth = pool.ReservePartition(8, 1);

        CollectHandles(second, handles);
        CollectHandles(third, handles);
        CollectHandles(fourth, handles);

        Assert.AreEqual(15, handles.Count);
        Assert.AreEqual(15, pool.LiveSetCount);
    }

    [TestMethod]
    public void ReusesReleasedRangeOfTheSameShape()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition first = pool.ReservePartition(2, 2);

        uint firstHandle = first.GetHandle(0).Value;

        pool.ReleasePartition(first);

        Assert.AreEqual(0, pool.LiveSetCount);
        Assert.AreEqual(0, pool.LiveEntryCount);

        int segmentCount = pool.SegmentCount;

        ResourceUsageSetPartition second = pool.ReservePartition(2, 2);

        Assert.AreEqual(segmentCount, pool.SegmentCount);
        Assert.AreEqual(firstHandle, second.GetHandle(0).Value);
        Assert.AreEqual(2, pool.LiveSetCount);
    }

    [TestMethod]
    public void DoesNotReuseRangeOfADifferentShape()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition first = pool.ReservePartition(2, 2);

        uint firstHandle = first.GetHandle(0).Value;

        pool.ReleasePartition(first);

        ResourceUsageSetPartition second = pool.ReservePartition(2, 3);

        Assert.AreNotEqual(firstHandle, second.GetHandle(0).Value);
    }

    [TestMethod]
    public void ClearsEveryTrackedUsageWhenAPartitionIsReleased()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition partition = pool.ReservePartition(1, 2);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(
            partition.Storage,
            ref partition.GetSet(0),
            Handle(1),
            0,
            new ResourceGenerationId(5),
            ComputeResourceAccess.Read,
            TrackedResourceState.UnorderedAccess,
            TrackedResourceState.UnorderedAccess,
            out _,
            out _));

        Assert.AreEqual(1, partition.GetSet(0).Count);

        pool.ReleasePartition(partition);

        Assert.AreEqual(0, partition.GetSet(0).Count);

        ResourceUsageSetPartition reused = pool.ReservePartition(1, 2);

        Assert.AreEqual(0, reused.GetSet(0).Count);
        Assert.IsTrue(reused.Storage[reused.GetSet(0).StorageOffset].Set.IsEmpty);
    }

    [TestMethod]
    public void TracksUsagesIndependentlyPerSetAndPartition()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition first = pool.ReservePartition(2, 1);
        ResourceUsageSetPartition second = pool.ReservePartition(2, 1);

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(
            first.Storage, ref first.GetSet(0), Handle(1), 0, new ResourceGenerationId(1), ComputeResourceAccess.Read, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess, out _, out _));

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(
            first.Storage, ref first.GetSet(1), Handle(1), 0, new ResourceGenerationId(2), ComputeResourceAccess.Write, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess, out _, out _));

        Assert.IsTrue(ResourceUsageTracker.TryAddUsage(
            second.Storage, ref second.GetSet(0), Handle(1), 0, new ResourceGenerationId(3), ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess, TrackedResourceState.UnorderedAccess, out _, out _));

        Assert.AreEqual(1, first.GetSet(0).Count);
        Assert.AreEqual(1, first.GetSet(1).Count);
        Assert.AreEqual(1, second.GetSet(0).Count);

        Assert.AreEqual(ComputeResourceAccess.Read, first.Storage[first.GetSet(0).StorageOffset].Access);
        Assert.AreEqual(ComputeResourceAccess.Write, first.Storage[first.GetSet(1).StorageOffset].Access);
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, second.Storage[second.GetSet(0).StorageOffset].Access);
    }

    [TestMethod]
    public void RejectsInvalidPartitionOperations()
    {
        ResourceUsageSetPool pool = new();

        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => pool.ReservePartition(-1, 1));
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => pool.ReservePartition(1, -1));
        _ = Assert.ThrowsExactly<ArgumentNullException>(() => pool.ReleasePartition(null!));

        ResourceUsageSetPartition partition = pool.ReservePartition(1, 1);

        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => partition.GetHandle(1));
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = partition.GetSet(1).Count);

        pool.ReleasePartition(partition);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => pool.ReleasePartition(partition));
    }

    [TestMethod]
    public void SupportsEmptyPartitions()
    {
        ResourceUsageSetPool pool = new();
        ResourceUsageSetPartition partition = pool.ReservePartition(0, 4);

        Assert.AreEqual(0, partition.SetCount);
        Assert.AreEqual(0, pool.LiveSetCount);

        pool.ReleasePartition(partition);

        Assert.AreEqual(0, pool.LiveSetCount);
    }
}
