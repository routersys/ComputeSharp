using System;

namespace ComputeSharp.Graphics.Pipelines;

internal struct HostRegistrationRecord
{
    public HostRegistrationId Id;

    public RegistrationState State;

    public int MaximumConcurrentInvocations;

    public int MaximumPendingSubmissions;

    public int MaximumTrackedResourceCount;

    public int MaximumCommandListSegments;

    public int OwnedSlotCount;

    public int ActiveInvocationCount;

    public int ReservedOrPendingCount;

    public HostRegistrationRecord(
        HostRegistrationId id,
        int maximumConcurrentInvocations,
        int maximumPendingSubmissions,
        int maximumTrackedResourceCount,
        int maximumCommandListSegments,
        int ownedSlotCount)
    {
        default(ArgumentException).ThrowIf(id.Value == 0, nameof(id));
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(maximumConcurrentInvocations);
        default(ArgumentOutOfRangeException).ThrowIfLessThan(maximumPendingSubmissions, maximumConcurrentInvocations);
        default(ArgumentOutOfRangeException).ThrowIfNegative(maximumTrackedResourceCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(maximumCommandListSegments);
        default(ArgumentOutOfRangeException).ThrowIfNegative(ownedSlotCount);

        this.Id = id;
        this.State = RegistrationState.Constructing;
        this.MaximumConcurrentInvocations = maximumConcurrentInvocations;
        this.MaximumPendingSubmissions = maximumPendingSubmissions;
        this.MaximumTrackedResourceCount = maximumTrackedResourceCount;
        this.MaximumCommandListSegments = maximumCommandListSegments;
        this.OwnedSlotCount = ownedSlotCount;
        this.ActiveInvocationCount = 0;
        this.ReservedOrPendingCount = 0;
    }

    public bool TryCommitActive()
    {
        if (this.State is not RegistrationState.Constructing)
        {
            return false;
        }

        this.State = RegistrationState.Active;

        return true;
    }

    public bool TryAbortConstruction()
    {
        if (this.State is not RegistrationState.Constructing)
        {
            return false;
        }

        this.State = RegistrationState.Released;

        return true;
    }

    public bool TryRequestDispose()
    {
        if (this.State is not RegistrationState.Active)
        {
            return false;
        }

        this.State = RegistrationState.DisposeRequested;

        return true;
    }

    public bool TryBeginRelease(bool isOwnedSlotDisposalComplete)
    {
        if (this.State is not RegistrationState.DisposeRequested ||
            this.ActiveInvocationCount != 0 ||
            this.ReservedOrPendingCount != 0 ||
            !isOwnedSlotDisposalComplete)
        {
            return false;
        }

        this.State = RegistrationState.Releasing;

        return true;
    }

    public bool TryCompleteRelease()
    {
        if (this.State is not RegistrationState.Releasing)
        {
            return false;
        }

        this.State = RegistrationState.Released;

        return true;
    }

    public bool TryAcquireInvocation()
    {
        if (this.State is not RegistrationState.Active || this.ActiveInvocationCount >= this.MaximumConcurrentInvocations)
        {
            return false;
        }

        this.ActiveInvocationCount++;

        return true;
    }

    public void ReleaseInvocation()
    {
        this.ActiveInvocationCount = Decrement(this.ActiveInvocationCount);
    }

    public bool TryReservePendingSubmission()
    {
        if (this.State is not RegistrationState.Active || this.ReservedOrPendingCount >= this.MaximumPendingSubmissions)
        {
            return false;
        }

        this.ReservedOrPendingCount++;

        return true;
    }

    public void ReleasePendingSubmission()
    {
        this.ReservedOrPendingCount = Decrement(this.ReservedOrPendingCount);
    }

    private static int Decrement(int value)
    {
        default(InvalidOperationException).ThrowIf(value <= 0, "The host registration reference count is already zero.");

        return value - 1;
    }
}
