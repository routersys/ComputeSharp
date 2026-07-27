namespace ComputeSharp.Interop;

internal struct DomainReferenceCounts
{
    public int Owner;

    public int ResourceSet;

    public int PersistentLease;

    public int TransientOperation;

    public int PendingTransaction;

    public int Maintenance;

    public DomainReferenceCounts()
    {
        this.Owner = 1;
        this.ResourceSet = 0;
        this.PersistentLease = 0;
        this.TransientOperation = 0;
        this.PendingTransaction = 0;
        this.Maintenance = 0;
    }

    public readonly bool IsZero =>
        this.Owner == 0 &&
        this.ResourceSet == 0 &&
        this.PersistentLease == 0 &&
        this.TransientOperation == 0 &&
        this.PendingTransaction == 0 &&
        this.Maintenance == 0;

    public bool TryAcquire(ExternalDomainReference reference)
    {
        switch (reference)
        {
            case ExternalDomainReference.Owner:
                return TryIncrement(ref this.Owner);
            case ExternalDomainReference.ResourceSet:
                return TryIncrement(ref this.ResourceSet);
            case ExternalDomainReference.PersistentLease:
                return TryIncrement(ref this.PersistentLease);
            case ExternalDomainReference.TransientOperation:
                return TryIncrement(ref this.TransientOperation);
            case ExternalDomainReference.PendingTransaction:
                return TryIncrement(ref this.PendingTransaction);
            case ExternalDomainReference.Maintenance:
                return TryIncrement(ref this.Maintenance);
            default:
                return false;
        }
    }

    public bool TryRelease(ExternalDomainReference reference)
    {
        switch (reference)
        {
            case ExternalDomainReference.Owner:
                return TryDecrement(ref this.Owner);
            case ExternalDomainReference.ResourceSet:
                return TryDecrement(ref this.ResourceSet);
            case ExternalDomainReference.PersistentLease:
                return TryDecrement(ref this.PersistentLease);
            case ExternalDomainReference.TransientOperation:
                return TryDecrement(ref this.TransientOperation);
            case ExternalDomainReference.PendingTransaction:
                return TryDecrement(ref this.PendingTransaction);
            case ExternalDomainReference.Maintenance:
                return TryDecrement(ref this.Maintenance);
            default:
                return false;
        }
    }

    private static bool TryIncrement(ref int count)
    {
        if (count == int.MaxValue)
        {
            return false;
        }

        count++;

        return true;
    }

    private static bool TryDecrement(ref int count)
    {
        if (count == 0)
        {
            return false;
        }

        count--;

        return true;
    }
}
