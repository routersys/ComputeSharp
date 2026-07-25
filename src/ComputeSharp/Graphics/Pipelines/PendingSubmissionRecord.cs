using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly record struct UsageSetHandle(uint Value)
{
    public bool IsNone => Value == 0;

    public int ToIndex()
    {
        default(InvalidOperationException).ThrowIf(Value == 0, "The usage set handle is none.");

        return checked((int)Value - 1);
    }

    public static UsageSetHandle FromIndex(int index)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(index);

        return new UsageSetHandle(checked((uint)index + 1));
    }
}

internal struct GraphicsResourceUsageEntry
{
    public ResourceGenerationSetHandle Set;

    public uint ResourceIndex;

    public ResourceGenerationId Generation;

    public ComputeResourceAccess Access;

    public TrackedResourceState FirstState;

    public TrackedResourceState FinalState;
}

internal struct UsageSetPoolEntry
{
    public int StorageOffset;

    public int Capacity;

    public int Count;
}

internal struct CommandListSegmentLease
{
    public nint CommandList;

    public nint CommandAllocator;

    public ComputeQueueKind Queue;

    public int IsValid;
}

internal struct CommandListLeaseSet
{
    public const int MaximumSegmentCount = 3;

    public byte Count;

    public CommandListSegmentLease Segment0;

    public CommandListSegmentLease Segment1;

    public CommandListSegmentLease Segment2;

    public static ref CommandListSegmentLease GetSegment(ref CommandListLeaseSet leaseSet, int index)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(index, 0, leaseSet.Count);

        switch (index)
        {
            case 0: return ref leaseSet.Segment0;
            case 1: return ref leaseSet.Segment1;
            default: return ref leaseSet.Segment2;
        }
    }

    public bool TryAdd(nint commandList, nint commandAllocator, ComputeQueueKind queue)
    {
        if (this.Count >= MaximumSegmentCount)
        {
            return false;
        }

        CommandListSegmentLease lease = new()
        {
            CommandList = commandList,
            CommandAllocator = commandAllocator,
            Queue = queue,
            IsValid = 1
        };

        switch (this.Count)
        {
            case 0:
                this.Segment0 = lease;
                break;
            case 1:
                this.Segment1 = lease;
                break;
            default:
                this.Segment2 = lease;
                break;
        }

        this.Count++;

        return true;
    }

    public void Clear()
    {
        this.Segment0 = default;
        this.Segment1 = default;
        this.Segment2 = default;
        this.Count = 0;
    }
}

internal struct InteropRetention
{
    public ExternalDomainId Domain;

    public int HoldsPendingTransactionReference;
}

internal struct SubmissionRetention
{
    public UsageSetHandle ResourceUsages;

    public CommandListLeaseSet CommandLists;

    public InteropRetention Interop;
}

internal struct PendingSubmissionRecord
{
    public SubmissionState State;

    public PipelineKey Pipeline;

    public FencePoint Completion;

    public SubmissionRetention Retention;

    public ulong SubmissionSequence;
}
