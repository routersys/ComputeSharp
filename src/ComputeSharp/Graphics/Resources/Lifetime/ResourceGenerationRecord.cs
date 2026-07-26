using System;
using System.Runtime.CompilerServices;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;

namespace ComputeSharp.Resources.Lifetime;

internal struct ResourceGenerationRecord
{
    public ResourceId ResourceId;

    public ResourceGenerationId Id;

    public ResourceGenerationState Lifecycle;

    public ExternalOwnershipState Ownership;

    public TrackedResourceState D3D12State;

    public FencePoint LastComputeRead;

    public FencePoint LastCopyRead;

    public FencePoint LastWrite;

    public FencePoint RetirementFence;

    public int OwnerReferenceCount;

    public int RecordingReferenceCount;

    public int PendingSubmissionReferenceCount;

    public int ExternalReferenceCount;

    public int CpuReferenceCount;

    public int PersistentLeaseActive;

    public int ExternalObjectsReleased;

    public MemoryPlacement Placement;

    public ComputeResourceRecovery Recovery;

    public ResourceReleaseAuthority ReleaseAuthority;

    public ulong LastUseSequence;

    public ulong ReclaimableBytes;

    public readonly bool HasReferences =>
        Volatile.Read(in this.OwnerReferenceCount) != 0 ||
        Volatile.Read(in this.RecordingReferenceCount) != 0 ||
        Volatile.Read(in this.PendingSubmissionReferenceCount) != 0 ||
        Volatile.Read(in this.ExternalReferenceCount) != 0 ||
        Volatile.Read(in this.CpuReferenceCount) != 0;

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

            if (HasReferences || !isRetirementFenceCompleted || Volatile.Read(in this.ExternalObjectsReleased) == 0)
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
                ResourceGenerationState.Faulted => authority is ResourceReleaseAuthority.DomainTeardown,
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

    public readonly ResourceGenerationState ReadLifecycle()
    {
        return (ResourceGenerationState)Volatile.Read(in Unsafe.As<ResourceGenerationState, int>(ref Unsafe.AsRef(in this.Lifecycle)));
    }

    private bool TryTransitionLifecycle(ResourceGenerationState expected, ResourceGenerationState next)
    {
        return Interlocked.CompareExchange(
            ref Unsafe.As<ResourceGenerationState, int>(ref this.Lifecycle),
            (int)next,
            (int)expected) == (int)expected;
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
