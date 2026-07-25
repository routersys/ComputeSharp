using System;
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
        this.OwnerReferenceCount != 0 ||
        this.RecordingReferenceCount != 0 ||
        this.PendingSubmissionReferenceCount != 0 ||
        this.ExternalReferenceCount != 0 ||
        this.CpuReferenceCount != 0;

    public bool TryAcquireRecordingReference()
    {
        if (this.Lifecycle is not ResourceGenerationState.Active)
        {
            return false;
        }

        this.RecordingReferenceCount = checked(this.RecordingReferenceCount + 1);

        return true;
    }

    public void ReleaseRecordingReference()
    {
        this.RecordingReferenceCount = Decrement(this.RecordingReferenceCount);
    }

    public void ConvertRecordingToPendingSubmission()
    {
        default(InvalidOperationException).ThrowIf(this.RecordingReferenceCount <= 0, "The resource generation has no recording reference to convert.");

        this.PendingSubmissionReferenceCount = checked(this.PendingSubmissionReferenceCount + 1);
        this.RecordingReferenceCount--;
    }

    public void ReleasePendingSubmissionReference()
    {
        this.PendingSubmissionReferenceCount = Decrement(this.PendingSubmissionReferenceCount);
    }

    public bool TryAcquireExternalReference()
    {
        if (this.Lifecycle is not ResourceGenerationState.Active)
        {
            return false;
        }

        this.ExternalReferenceCount = checked(this.ExternalReferenceCount + 1);

        return true;
    }

    public void ReleaseExternalReference()
    {
        this.ExternalReferenceCount = Decrement(this.ExternalReferenceCount);
    }

    public bool TryAcquireCpuReference()
    {
        if (this.Lifecycle is not ResourceGenerationState.Active)
        {
            return false;
        }

        this.CpuReferenceCount = checked(this.CpuReferenceCount + 1);

        return true;
    }

    public void ReleaseCpuReference()
    {
        this.CpuReferenceCount = Decrement(this.CpuReferenceCount);
    }

    public void ReleaseOwnerReference()
    {
        this.OwnerReferenceCount = Decrement(this.OwnerReferenceCount);
    }

    public bool TryRequestRetire()
    {
        if (this.Lifecycle is not ResourceGenerationState.Active)
        {
            return false;
        }

        this.Lifecycle = ResourceGenerationState.RetireRequested;

        return true;
    }

    public bool TryPromoteRetiredReady(bool isRetirementFenceCompleted)
    {
        if (this.Lifecycle is not (ResourceGenerationState.RetireRequested or ResourceGenerationState.RetiredPending))
        {
            return false;
        }

        if (HasReferences || !isRetirementFenceCompleted || this.ExternalObjectsReleased == 0)
        {
            this.Lifecycle = ResourceGenerationState.RetiredPending;

            return false;
        }

        this.Lifecycle = ResourceGenerationState.RetiredReady;

        return true;
    }

    public bool TryBeginRelease(ResourceReleaseAuthority authority)
    {
        bool isAuthorized = this.Lifecycle switch
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

        this.ReleaseAuthority = authority;
        this.Lifecycle = ResourceGenerationState.Releasing;

        return true;
    }

    public bool TryCompleteRelease(ResourceReleaseAuthority authority)
    {
        if (this.Lifecycle is not ResourceGenerationState.Releasing || this.ReleaseAuthority != authority)
        {
            return false;
        }

        this.Lifecycle = ResourceGenerationState.Released;

        return true;
    }

    public bool TryMarkTerminalRetained()
    {
        if (this.Lifecycle is ResourceGenerationState.Releasing or ResourceGenerationState.Released)
        {
            return false;
        }

        this.Lifecycle = ResourceGenerationState.TerminalRetained;

        return true;
    }

    private static int Decrement(int value)
    {
        default(InvalidOperationException).ThrowIf(value <= 0, "The resource generation reference count is already zero.");

        return value - 1;
    }
}
