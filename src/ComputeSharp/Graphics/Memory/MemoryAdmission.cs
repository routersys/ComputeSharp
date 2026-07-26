namespace ComputeSharp.Memory;

internal enum MemoryAdmissionStatus : byte
{
    Admitted = 0,
    SegmentInactive = 1,
    BudgetUnavailable = 2,
    BudgetExceeded = 3,
    GrantUnavailable = 4,
    GrantExceeded = 5,
    ExplicitLimitExceeded = 6,
    ArithmeticOverflow = 7,
    StaleSnapshot = 8
}

internal static class MemoryAdmission
{
    public static MemoryAdmissionStatus Evaluate(
        in SegmentPolicySnapshot segment,
        in SegmentMemoryAccounting accounting,
        ulong requestedBytes)
    {
        if (!segment.TopologyActive)
        {
            return MemoryAdmissionStatus.SegmentInactive;
        }

        if (segment.DxgiStatus is not MemoryBudgetStatus.Valid)
        {
            return MemoryAdmissionStatus.BudgetUnavailable;
        }

        if (!TryAdd(segment.Dxgi.CurrentUsageBytes, accounting.ReservationBytes, requestedBytes, out ulong processProjected) ||
            !TryAdd(accounting.OwnedBytes, accounting.ReservationBytes, requestedBytes, out ulong ownedProjected))
        {
            return MemoryAdmissionStatus.ArithmeticOverflow;
        }

        if (processProjected > segment.Dxgi.BudgetBytes)
        {
            return MemoryAdmissionStatus.BudgetExceeded;
        }

        if (segment.BrokerConfigured)
        {
            if (segment.GrantStatus is not BrokerGrantStatus.Valid)
            {
                return MemoryAdmissionStatus.GrantUnavailable;
            }

            if (segment.Grant.HasLimit && ownedProjected > segment.Grant.LimitBytes)
            {
                return MemoryAdmissionStatus.GrantExceeded;
            }
        }

        if (segment.ExplicitHardLimitBytes is { } explicitHardLimitBytes && ownedProjected > explicitHardLimitBytes)
        {
            return MemoryAdmissionStatus.ExplicitLimitExceeded;
        }

        return MemoryAdmissionStatus.Admitted;
    }

    public static bool IsGrantObservationValid(in GraphicsMemoryGrant previous, in GraphicsMemoryGrant current)
    {
        if (current.Version < previous.Version)
        {
            return false;
        }

        if (current.Version != previous.Version)
        {
            return true;
        }

        return current.HasLimit == previous.HasLimit &&
            (!current.HasLimit || current.LimitBytes == previous.LimitBytes);
    }

    private static bool TryAdd(ulong first, ulong second, ulong third, out ulong result)
    {
        result = 0;

        ulong partial = first + second;

        if (partial < first)
        {
            return false;
        }

        ulong total = partial + third;

        if (total < partial)
        {
            return false;
        }

        result = total;

        return true;
    }
}
