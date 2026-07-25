using System;

namespace ComputeSharp.Graphics.Pipelines;

internal struct ResourceSetRegistrationRecord
{
    private const int MaximumPersistentLeasesPerSlot = 2;

    public ResourceSetRegistrationId Id;

    public RegistrationState State;

    public int SharedTextureSlotCount;

    public int PersistentLeaseCount;

    public int ActiveMaintenanceCount;

    public ResourceSetRegistrationRecord(ResourceSetRegistrationId id, int sharedTextureSlotCount)
    {
        default(ArgumentException).ThrowIf(id.Value == 0, nameof(id));
        default(ArgumentOutOfRangeException).ThrowIfNegative(sharedTextureSlotCount);

        this.Id = id;
        this.State = RegistrationState.Constructing;
        this.SharedTextureSlotCount = sharedTextureSlotCount;
        this.PersistentLeaseCount = 0;
        this.ActiveMaintenanceCount = 0;
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

    public bool TryBeginRelease(bool isSharedSlotDisposalComplete)
    {
        if (this.State is not RegistrationState.DisposeRequested ||
            this.PersistentLeaseCount != 0 ||
            this.ActiveMaintenanceCount != 0 ||
            !isSharedSlotDisposalComplete)
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

    public bool TryAcquirePersistentLease()
    {
        if (this.State is not RegistrationState.Active ||
            this.PersistentLeaseCount >= checked(MaximumPersistentLeasesPerSlot * this.SharedTextureSlotCount))
        {
            return false;
        }

        this.PersistentLeaseCount++;

        return true;
    }

    public void ReleasePersistentLease()
    {
        this.PersistentLeaseCount = Decrement(this.PersistentLeaseCount);
    }

    public bool TryRegisterMaintenance()
    {
        if (this.State is not (RegistrationState.Active or RegistrationState.DisposeRequested) ||
            this.ActiveMaintenanceCount >= this.SharedTextureSlotCount)
        {
            return false;
        }

        this.ActiveMaintenanceCount++;

        return true;
    }

    public void CompleteMaintenance()
    {
        this.ActiveMaintenanceCount = Decrement(this.ActiveMaintenanceCount);
    }

    private static int Decrement(int value)
    {
        default(InvalidOperationException).ThrowIf(value <= 0, "The resource set registration reference count is already zero.");

        return value - 1;
    }
}
