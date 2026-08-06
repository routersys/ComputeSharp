using System;

using System.Threading;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Memory;

namespace ComputeWeave.Resources.Lifetime;

internal struct ResourceGenerationRecord
{
    internal const int PersistentLeaseActiveBit = 1 << 0;
    internal const int ExternalObjectsReleasedBit = 1 << 1;

    internal const int LifecycleShift = 2;
    internal const int LifecycleMask = 0xF << LifecycleShift;

    internal const int OwnershipShift = 6;
    internal const int OwnershipMask = 0x7 << OwnershipShift;

    internal const int D3D12StateShift = 9;
    internal const int D3D12StateMask = 0x7 << D3D12StateShift;

    internal const int PlacementShift = 12;
    internal const int PlacementMask = 0x1 << PlacementShift;

    internal const int RecoveryShift = 13;
    internal const int RecoveryMask = 0x3 << RecoveryShift;

    internal const int ReleaseAuthorityShift = 15;
    internal const int ReleaseAuthorityMask = 0x3 << ReleaseAuthorityShift;

    internal const int LastComputeReadQueueShift = 17;
    internal const int LastComputeReadQueueMask = 0x3 << LastComputeReadQueueShift;

    internal const int LastCopyReadQueueShift = 19;
    internal const int LastCopyReadQueueMask = 0x3 << LastCopyReadQueueShift;

    internal const int LastWriteQueueShift = 21;
    internal const int LastWriteQueueMask = 0x3 << LastWriteQueueShift;

    internal const int RetirementFenceQueueShift = 23;
    internal const int RetirementFenceQueueMask = 0x3 << RetirementFenceQueueShift;

    public ulong LastUseSequence;

    public ulong ReclaimableBytes;

    public ResourceId ResourceId;

    public ResourceGenerationId Id;

    public ulong LastComputeReadValue;

    public ulong LastCopyReadValue;

    public ulong LastWriteValue;

    public ulong RetirementFenceValue;

    public int OwnerReferenceCount;

    public int RecordingReferenceCount;

    public int PendingSubmissionReferenceCount;

    public int ExternalReferenceCount;

    public int CpuReferenceCount;

    public int NativeReferenceCount;

    public int StateFlags;

    public ResourceGenerationState Lifecycle
    {
        readonly get => (ResourceGenerationState)((Volatile.Read(in this.StateFlags) & LifecycleMask) >> LifecycleShift);
        set => SetStateBits(LifecycleMask, LifecycleShift, (int)value);
    }

    public ExternalOwnershipState Ownership
    {
        readonly get => (ExternalOwnershipState)((Volatile.Read(in this.StateFlags) & OwnershipMask) >> OwnershipShift);
        set => SetStateBits(OwnershipMask, OwnershipShift, (int)value);
    }

    public TrackedResourceState D3D12State
    {
        readonly get => (TrackedResourceState)((Volatile.Read(in this.StateFlags) & D3D12StateMask) >> D3D12StateShift);
        set => SetStateBits(D3D12StateMask, D3D12StateShift, (int)value);
    }

    public MemoryPlacement Placement
    {
        readonly get => (MemoryPlacement)((Volatile.Read(in this.StateFlags) & PlacementMask) >> PlacementShift);
        set => SetStateBits(PlacementMask, PlacementShift, (int)value);
    }

    public ComputeResourceRecovery Recovery
    {
        readonly get => (ComputeResourceRecovery)((Volatile.Read(in this.StateFlags) & RecoveryMask) >> RecoveryShift);
        set => SetStateBits(RecoveryMask, RecoveryShift, (int)value);
    }

    public ResourceReleaseAuthority ReleaseAuthority
    {
        readonly get => (ResourceReleaseAuthority)((Volatile.Read(in this.StateFlags) & ReleaseAuthorityMask) >> ReleaseAuthorityShift);
        set => SetStateBits(ReleaseAuthorityMask, ReleaseAuthorityShift, (int)value);
    }

    public FencePoint LastComputeRead
    {
        readonly get
        {
            int flags = Volatile.Read(in this.StateFlags);
            ulong fenceValue = Volatile.Read(in this.LastComputeReadValue);
            return new((ComputeQueueKind)((flags & LastComputeReadQueueMask) >> LastComputeReadQueueShift), fenceValue);
        }
        set
        {
            Volatile.Write(ref this.LastComputeReadValue, value.Value);
            SetStateBits(LastComputeReadQueueMask, LastComputeReadQueueShift, (int)value.Queue);
        }
    }

    public FencePoint LastCopyRead
    {
        readonly get
        {
            int flags = Volatile.Read(in this.StateFlags);
            ulong fenceValue = Volatile.Read(in this.LastCopyReadValue);
            return new((ComputeQueueKind)((flags & LastCopyReadQueueMask) >> LastCopyReadQueueShift), fenceValue);
        }
        set
        {
            Volatile.Write(ref this.LastCopyReadValue, value.Value);
            SetStateBits(LastCopyReadQueueMask, LastCopyReadQueueShift, (int)value.Queue);
        }
    }

    public FencePoint LastWrite
    {
        readonly get
        {
            int flags = Volatile.Read(in this.StateFlags);
            ulong fenceValue = Volatile.Read(in this.LastWriteValue);
            return new((ComputeQueueKind)((flags & LastWriteQueueMask) >> LastWriteQueueShift), fenceValue);
        }
        set
        {
            Volatile.Write(ref this.LastWriteValue, value.Value);
            SetStateBits(LastWriteQueueMask, LastWriteQueueShift, (int)value.Queue);
        }
    }

    public FencePoint RetirementFence
    {
        readonly get
        {
            int flags = Volatile.Read(in this.StateFlags);
            ulong fenceValue = Volatile.Read(in this.RetirementFenceValue);
            return new((ComputeQueueKind)((flags & RetirementFenceQueueMask) >> RetirementFenceQueueShift), fenceValue);
        }
        set
        {
            Volatile.Write(ref this.RetirementFenceValue, value.Value);
            SetStateBits(RetirementFenceQueueMask, RetirementFenceQueueShift, (int)value.Queue);
        }
    }

    private void SetStateBits(int mask, int shift, int value)
    {
        int current;

        do
        {
            current = Volatile.Read(ref this.StateFlags);
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, (current & ~mask) | (value << shift), current) != current);
    }

    public readonly bool HasQueueReferences =>
        Volatile.Read(in this.RecordingReferenceCount) != 0 ||
        Volatile.Read(in this.PendingSubmissionReferenceCount) != 0;

    public readonly bool HasReferences =>
        Volatile.Read(in this.OwnerReferenceCount) != 0 ||
        Volatile.Read(in this.RecordingReferenceCount) != 0 ||
        Volatile.Read(in this.PendingSubmissionReferenceCount) != 0 ||
        Volatile.Read(in this.ExternalReferenceCount) != 0 ||
        Volatile.Read(in this.CpuReferenceCount) != 0 ||
        Volatile.Read(in this.NativeReferenceCount) != 0;

    public readonly bool IsIdle =>
        ReadLifecycle() is ResourceGenerationState.Active &&
        Volatile.Read(in this.RecordingReferenceCount) == 0 &&
        Volatile.Read(in this.PendingSubmissionReferenceCount) == 0 &&
        Volatile.Read(in this.ExternalReferenceCount) == 0 &&
        Volatile.Read(in this.CpuReferenceCount) == 0 &&
        Volatile.Read(in this.NativeReferenceCount) == 0 &&
        (Volatile.Read(in this.StateFlags) & PersistentLeaseActiveBit) == 0;

    public readonly bool IsExternalObjectsReleased =>
        (Volatile.Read(in this.StateFlags) & ExternalObjectsReleasedBit) != 0;

    public bool TryAcquireRecordingReference()
    {
        if (ReadLifecycle() is not ResourceGenerationState.Active)
        {
            return false;
        }

        Increment(ref this.RecordingReferenceCount);

        return true;
    }

    public void ReleaseRecordingReference()
    {
        Decrement(ref this.RecordingReferenceCount);
    }

    public void ConvertRecordingToPendingSubmission()
    {
        default(InvalidOperationException).ThrowIf(
            Volatile.Read(in this.RecordingReferenceCount) <= 0,
            "The resource generation has no recording reference to convert.");

        Increment(ref this.PendingSubmissionReferenceCount);
        Decrement(ref this.RecordingReferenceCount);
    }

    public void ReleasePendingSubmissionReference()
    {
        Decrement(ref this.PendingSubmissionReferenceCount);
    }

    public bool TryAcquireExternalReference()
    {
        if (ReadLifecycle() is not ResourceGenerationState.Active)
        {
            return false;
        }

        Increment(ref this.ExternalReferenceCount);

        return true;
    }

    public void ReleaseExternalReference()
    {
        Decrement(ref this.ExternalReferenceCount);
    }

    public bool TryAcquirePersistentLease()
    {
        int current;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            if ((current & PersistentLeaseActiveBit) != 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, current | PersistentLeaseActiveBit, current) != current);

        return true;
    }

    public void ReleasePersistentLease()
    {
        int current;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            default(InvalidOperationException).ThrowIf(
                (current & PersistentLeaseActiveBit) == 0,
                "The resource generation holds no persistent lease.");
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, current & ~PersistentLeaseActiveBit, current) != current);
    }

    public bool TryAcquireCpuReference()
    {
        if (ReadLifecycle() is not ResourceGenerationState.Active)
        {
            return false;
        }

        Increment(ref this.CpuReferenceCount);

        return true;
    }

    public void ReleaseCpuReference()
    {
        Decrement(ref this.CpuReferenceCount);
    }

    public bool TryAcquireNativeReference()
    {
        if (ReadLifecycle() is not ResourceGenerationState.Active)
        {
            return false;
        }

        Increment(ref this.NativeReferenceCount);

        return true;
    }

    public void ReleaseNativeReference()
    {
        Decrement(ref this.NativeReferenceCount);
    }

    public void MarkExternalObjectsReleased()
    {
        int current;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            if ((current & ExternalObjectsReleasedBit) != 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, current | ExternalObjectsReleasedBit, current) != current);
    }

    public void ReleaseOwnerReference()
    {
        Decrement(ref this.OwnerReferenceCount);
    }

    public bool TryCompleteConstruction()
    {
        if (ReadLifecycle() is not ResourceGenerationState.Constructing)
        {
            return false;
        }

        Increment(ref this.OwnerReferenceCount);

        if (TryTransitionLifecycle(ResourceGenerationState.Constructing, ResourceGenerationState.Active))
        {
            return true;
        }

        Decrement(ref this.OwnerReferenceCount);

        return false;
    }

    public bool TryFailConstruction()
    {
        return TryTransitionLifecycle(ResourceGenerationState.Constructing, ResourceGenerationState.Released);
    }

    public bool TryRequestRetire()
    {
        return TryTransitionLifecycle(ResourceGenerationState.Active, ResourceGenerationState.RetireRequested);
    }

    public bool TryPromoteRetiredReady(bool isRetirementFenceCompleted)
    {
        while (true)
        {
            ResourceGenerationState current = ReadLifecycle();

            if (current is not (ResourceGenerationState.RetireRequested or ResourceGenerationState.RetiredPending))
            {
                return false;
            }

            if (HasReferences || !isRetirementFenceCompleted || !IsExternalObjectsReleased)
            {
                _ = TryTransitionLifecycle(ResourceGenerationState.RetireRequested, ResourceGenerationState.RetiredPending);

                return false;
            }

            if (TryTransitionLifecycle(current, ResourceGenerationState.RetiredReady))
            {
                return true;
            }
        }
    }

    public bool TryBeginRelease(ResourceReleaseAuthority authority)
    {
        while (true)
        {
            ResourceGenerationState current = ReadLifecycle();

            bool isAuthorized = current switch
            {
                ResourceGenerationState.RetiredReady => authority is ResourceReleaseAuthority.NormalCompletion,
                ResourceGenerationState.Faulted =>
                    authority is ResourceReleaseAuthority.DomainTeardown &&
                    !HasQueueReferences &&
                    IsExternalObjectsReleased,
                ResourceGenerationState.TerminalRetained => authority is ResourceReleaseAuthority.DeviceTeardown,
                _ => false
            };

            if (!isAuthorized)
            {
                return false;
            }

            if (TryTransitionLifecycle(current, ResourceGenerationState.Releasing))
            {
                this.ReleaseAuthority = authority;

                return true;
            }
        }
    }

    public bool TryCompleteRelease(ResourceReleaseAuthority authority)
    {
        if (this.ReleaseAuthority != authority)
        {
            return false;
        }

        return TryTransitionLifecycle(ResourceGenerationState.Releasing, ResourceGenerationState.Released);
    }

    public bool TryMarkFaulted()
    {
        return TryTransitionLifecycle(ResourceGenerationState.Active, ResourceGenerationState.Faulted);
    }

    public bool TryMarkTerminalRetained()
    {
        while (true)
        {
            ResourceGenerationState current = ReadLifecycle();

            if (current is ResourceGenerationState.Releasing or ResourceGenerationState.Released)
            {
                return false;
            }

            if (current is ResourceGenerationState.TerminalRetained ||
                TryTransitionLifecycle(current, ResourceGenerationState.TerminalRetained))
            {
                return true;
            }
        }
    }

    public bool TryMarkAcquireSignalEnqueued()
    {
        return TryTransitionOwnership(ExternalOwnershipState.ExternalAvailable, ExternalOwnershipState.AcquireSignalEnqueued);
    }

    public bool TryMarkComputeExecutionIssued()
    {
        int current;
        int computeAvailableBits = (int)ExternalOwnershipState.ComputeAvailable << OwnershipShift;
        int acquireSignalBits = (int)ExternalOwnershipState.AcquireSignalEnqueued << OwnershipShift;
        int executionIssuedBits = (int)ExternalOwnershipState.ComputeExecutionIssued << OwnershipShift;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            int ownershipBits = current & OwnershipMask;

            if (ownershipBits != computeAvailableBits && ownershipBits != acquireSignalBits)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, (current & ~OwnershipMask) | executionIssuedBits, current) != current);

        return true;
    }

    public bool TryMarkReleaseSignalEnqueued()
    {
        return TryTransitionOwnership(ExternalOwnershipState.ComputeExecutionIssued, ExternalOwnershipState.ReleaseSignalEnqueued);
    }

    public bool TryMarkExternalAvailable()
    {
        return TryTransitionOwnership(ExternalOwnershipState.ReleaseSignalEnqueued, ExternalOwnershipState.ExternalAvailable);
    }

    public bool TryMarkOwnershipFaulted()
    {
        int faultedBits = (int)ExternalOwnershipState.Faulted << OwnershipShift;
        int current;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            if ((current & OwnershipMask) == faultedBits)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, (current & ~OwnershipMask) | faultedBits, current) != current);

        return true;
    }

    public readonly ResourceGenerationState ReadLifecycle()
    {
        return (ResourceGenerationState)((Volatile.Read(in this.StateFlags) & LifecycleMask) >> LifecycleShift);
    }

    public readonly ExternalOwnershipState ReadOwnership()
    {
        return (ExternalOwnershipState)((Volatile.Read(in this.StateFlags) & OwnershipMask) >> OwnershipShift);
    }

    private bool TryTransitionOwnership(ExternalOwnershipState expected, ExternalOwnershipState next)
    {
        int current;
        int expectedBits = (int)expected << OwnershipShift;
        int nextBits = (int)next << OwnershipShift;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            if ((current & OwnershipMask) != expectedBits)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, (current & ~OwnershipMask) | nextBits, current) != current);

        return true;
    }

    private bool TryTransitionLifecycle(ResourceGenerationState expected, ResourceGenerationState next)
    {
        int current;
        int expectedBits = (int)expected << LifecycleShift;
        int nextBits = (int)next << LifecycleShift;

        do
        {
            current = Volatile.Read(ref this.StateFlags);

            if ((current & LifecycleMask) != expectedBits)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.StateFlags, (current & ~LifecycleMask) | nextBits, current) != current);

        return true;
    }

    private static void Increment(ref int count)
    {
        int current;

        do
        {
            current = Volatile.Read(ref count);

            _ = checked(current + 1);
        }
        while (Interlocked.CompareExchange(ref count, current + 1, current) != current);
    }

    private static void Decrement(ref int count)
    {
        int current;

        do
        {
            current = Volatile.Read(ref count);

            default(InvalidOperationException).ThrowIf(current <= 0, "The resource generation reference count is already zero.");
        }
        while (Interlocked.CompareExchange(ref count, current - 1, current) != current);
    }
}

