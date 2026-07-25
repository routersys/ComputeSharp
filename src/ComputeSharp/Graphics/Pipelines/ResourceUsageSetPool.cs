using System;
using System.Collections.Generic;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed class ResourceUsageSetPartition
{
    private readonly UsageSetPoolEntry[] sets;

    private readonly GraphicsResourceUsageEntry[] entries;

    internal ResourceUsageSetPartition(
        UsageSetPoolEntry[] sets,
        GraphicsResourceUsageEntry[] entries,
        int setOffset,
        int setCount,
        int entryOffset,
        int entryCapacity,
        int globalSetBase)
    {
        this.sets = sets;
        this.entries = entries;

        SetOffset = setOffset;
        SetCount = setCount;
        EntryOffset = entryOffset;
        EntryCapacity = entryCapacity;
        GlobalSetBase = globalSetBase;

        for (int i = 0; i < setCount; i++)
        {
            this.sets[checked(setOffset + i)] = new UsageSetPoolEntry
            {
                StorageOffset = checked(entryOffset + (i * entryCapacity)),
                Capacity = entryCapacity,
                Count = 0
            };
        }
    }

    public int SetOffset { get; }

    public int SetCount { get; }

    public int EntryOffset { get; }

    public int EntryCapacity { get; }

    public int GlobalSetBase { get; }

    public Span<GraphicsResourceUsageEntry> Storage => this.entries;

    public UsageSetHandle GetHandle(int setIndex)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(setIndex, 0, SetCount);

        return UsageSetHandle.FromIndex(checked(GlobalSetBase + setIndex));
    }

    public int GetSetIndex(UsageSetHandle handle)
    {
        int setIndex = checked(handle.ToIndex() - GlobalSetBase);

        default(ArgumentOutOfRangeException).ThrowIfNotInRange(setIndex, 0, SetCount);

        return setIndex;
    }

    public ref UsageSetPoolEntry GetSet(int setIndex)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(setIndex, 0, SetCount);

        return ref this.sets[checked(SetOffset + setIndex)];
    }

    public void ClearAllUsages()
    {
        for (int i = 0; i < SetCount; i++)
        {
            ResourceUsageTracker.ClearUsages(this.entries, ref GetSet(i));
        }
    }
}

internal sealed class ResourceUsageSetPool
{
    private sealed class Segment(UsageSetPoolEntry[] sets, GraphicsResourceUsageEntry[] entries, int globalSetBase)
    {
        public UsageSetPoolEntry[] Sets { get; } = sets;

        public GraphicsResourceUsageEntry[] Entries { get; } = entries;

        public int GlobalSetBase { get; } = globalSetBase;
    }

    private readonly record struct ReleasedRange(
        int SetCount,
        int EntryCapacity,
        Segment Segment,
        int SetOffset,
        int EntryOffset,
        int GlobalSetBase);

    private readonly List<Segment> segments = [];

    private readonly List<ReleasedRange> releasedRanges = [];

    private readonly object gate = new();

    private int nextGlobalSetBase;

    private int liveSetCount;

    private int liveEntryCount;

    public int LiveSetCount
    {
        get
        {
            lock (this.gate)
            {
                return this.liveSetCount;
            }
        }
    }

    public int LiveEntryCount
    {
        get
        {
            lock (this.gate)
            {
                return this.liveEntryCount;
            }
        }
    }

    public int SegmentCount
    {
        get
        {
            lock (this.gate)
            {
                return this.segments.Count;
            }
        }
    }

    public ResourceUsageSetPartition ReservePartition(int setCount, int entryCapacity)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(setCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(entryCapacity);

        int entryCount = checked(setCount * entryCapacity);

        lock (this.gate)
        {
            this.liveSetCount = checked(this.liveSetCount + setCount);
            this.liveEntryCount = checked(this.liveEntryCount + entryCount);

            for (int i = 0; i < this.releasedRanges.Count; i++)
            {
                ReleasedRange released = this.releasedRanges[i];

                if (released.SetCount != setCount || released.EntryCapacity != entryCapacity)
                {
                    continue;
                }

                this.releasedRanges.RemoveAt(i);

                return new ResourceUsageSetPartition(
                    released.Segment.Sets,
                    released.Segment.Entries,
                    released.SetOffset,
                    setCount,
                    released.EntryOffset,
                    entryCapacity,
                    released.GlobalSetBase);
            }

            Segment segment = CreateSegment(setCount, entryCount);

            return new ResourceUsageSetPartition(
                segment.Sets,
                segment.Entries,
                0,
                setCount,
                0,
                entryCapacity,
                segment.GlobalSetBase);
        }
    }

    public void ReleasePartition(ResourceUsageSetPartition partition)
    {
        default(ArgumentNullException).ThrowIfNull(partition);

        partition.ClearAllUsages();

        lock (this.gate)
        {
            this.liveSetCount = Subtract(this.liveSetCount, partition.SetCount);
            this.liveEntryCount = Subtract(this.liveEntryCount, checked(partition.SetCount * partition.EntryCapacity));

            if (partition.SetCount == 0)
            {
                return;
            }

            Segment? owner = null;

            foreach (Segment segment in this.segments)
            {
                if (partition.GlobalSetBase >= segment.GlobalSetBase &&
                    checked(partition.GlobalSetBase + partition.SetCount) <= checked(segment.GlobalSetBase + segment.Sets.Length))
                {
                    owner = segment;

                    break;
                }
            }

            default(ArgumentException).ThrowIf(owner is null, nameof(partition));

            this.releasedRanges.Add(new ReleasedRange(
                partition.SetCount,
                partition.EntryCapacity,
                owner!,
                partition.SetOffset,
                partition.EntryOffset,
                partition.GlobalSetBase));
        }
    }

    private Segment CreateSegment(int setCount, int entryCount)
    {
        Segment segment = new(
            new UsageSetPoolEntry[setCount],
            new GraphicsResourceUsageEntry[entryCount],
            this.nextGlobalSetBase);

        this.segments.Add(segment);

        this.nextGlobalSetBase = checked(this.nextGlobalSetBase + setCount);

        return segment;
    }

    private static int Subtract(int current, int delta)
    {
        default(InvalidOperationException).ThrowIf(delta > current, "The usage set pool is below the released partition.");

        return current - delta;
    }
}
