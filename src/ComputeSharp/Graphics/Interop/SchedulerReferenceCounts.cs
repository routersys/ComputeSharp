using System;

namespace ComputeSharp.Interop;

internal struct SchedulerReferenceCounts
{
    public int Owner;

    public int Registration;

    public int ActiveReservation;

    public SchedulerReferenceCounts()
    {
        this.Owner = 1;
        this.Registration = 0;
        this.ActiveReservation = 0;
    }

    public readonly bool IsReleased => this.Owner == 0 && this.Registration == 0 && this.ActiveReservation == 0;

    public bool TryReleaseOwner()
    {
        if (this.Owner == 0)
        {
            return false;
        }

        this.Owner = 0;

        return true;
    }

    public bool TryAcquireRegistration()
    {
        if (this.Owner == 0 || this.Registration == int.MaxValue)
        {
            return false;
        }

        this.Registration++;

        return true;
    }

    public void ReleaseRegistration()
    {
        this.Registration = Decrement(this.Registration);
    }

    public void AcquireReservation()
    {
        default(InvalidOperationException).ThrowIf(
            this.Registration == 0,
            "The external queue scheduler holds no registration to reserve from.");

        this.ActiveReservation = checked(this.ActiveReservation + 1);
    }

    public void ReleaseReservation()
    {
        this.ActiveReservation = Decrement(this.ActiveReservation);
    }

    private static int Decrement(int value)
    {
        default(InvalidOperationException).ThrowIf(value <= 0, "The external queue scheduler reference count is already zero.");

        return value - 1;
    }
}
