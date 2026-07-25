using System;
using System.Runtime.CompilerServices;
using System.Threading;
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

    public readonly SubmissionState ReadState()
    {
        return (SubmissionState)Volatile.Read(in Unsafe.As<SubmissionState, int>(ref Unsafe.AsRef(in this.State)));
    }

    public bool TryReserve(PipelineKey pipeline, ulong submissionSequence)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotZero(submissionSequence == 0 ? 1 : 0, nameof(submissionSequence));

        if (!TryTransition(SubmissionState.Returned, SubmissionState.Reserved))
        {
            return false;
        }

        this.Pipeline = pipeline;
        this.SubmissionSequence = submissionSequence;
        this.Completion = FencePoint.None;
        this.Retention = default;

        return true;
    }

    public bool TryBeginRecording()
    {
        return TryTransition(SubmissionState.Reserved, SubmissionState.Recording);
    }

    public bool TryCompleteValidation()
    {
        return TryTransition(SubmissionState.Recording, SubmissionState.Prepared);
    }

    public bool TryMarkExecutionIssued()
    {
        return TryTransition(SubmissionState.Prepared, SubmissionState.ExecutionIssued);
    }

    public bool TryMarkCompletionSignaled()
    {
        return TryTransition(SubmissionState.ExecutionIssued, SubmissionState.CompletionSignaled);
    }

    public bool TryCommitHazards()
    {
        return TryTransition(SubmissionState.CompletionSignaled, SubmissionState.HazardCommitted);
    }

    public bool TryCommitAndPublish(FencePoint completion, in SubmissionRetention retention)
    {
        if (ReadState() is not SubmissionState.HazardCommitted)
        {
            return false;
        }

        this.Completion = completion;
        this.Retention = retention;

        if (TryTransition(SubmissionState.HazardCommitted, SubmissionState.Committed))
        {
            return true;
        }

        this.Completion = FencePoint.None;
        this.Retention = default;

        return false;
    }

    public bool TryMarkCompletionReady()
    {
        return TryTransition(SubmissionState.Committed, SubmissionState.CompletionReady);
    }

    public bool TryClaimForReturn()
    {
        return TryTransition(SubmissionState.CompletionReady, SubmissionState.Returning);
    }

    public bool TryDetachRetention(out SubmissionRetention retention)
    {
        if (ReadState() is not SubmissionState.Returning)
        {
            retention = default;

            return false;
        }

        retention = this.Retention;

        this.Retention = default;

        return true;
    }

    public bool TryCompleteReturn()
    {
        if (!TryTransition(SubmissionState.Returning, SubmissionState.Returned))
        {
            return false;
        }

        this.Pipeline = default;
        this.Completion = FencePoint.None;
        this.Retention = default;
        this.SubmissionSequence = 0;

        return true;
    }

    public bool TryAbort()
    {
        while (true)
        {
            SubmissionState current = ReadState();

            if (current is not (SubmissionState.Reserved or SubmissionState.Recording or SubmissionState.Prepared))
            {
                return false;
            }

            if (TryTransition(current, SubmissionState.Returned))
            {
                this.Pipeline = default;
                this.Completion = FencePoint.None;
                this.Retention = default;
                this.SubmissionSequence = 0;

                return true;
            }
        }
    }

    public bool TryMarkTerminalRetained()
    {
        while (true)
        {
            SubmissionState current = ReadState();

            if (current is not (SubmissionState.ExecutionIssued or SubmissionState.CompletionSignaled))
            {
                return false;
            }

            if (TryTransition(current, SubmissionState.TerminalRetained))
            {
                return true;
            }
        }
    }

    private bool TryTransition(SubmissionState expected, SubmissionState next)
    {
        return Interlocked.CompareExchange(
            ref Unsafe.As<SubmissionState, int>(ref this.State),
            (int)next,
            (int)expected) == (int)expected;
    }
}
