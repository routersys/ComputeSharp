namespace ComputeSharp.Memory;

internal enum MemoryBudgetStatus : byte
{
    Unknown = 0,
    Valid = 1,
    Unsupported = 2,
    DeviceLost = 3
}

internal readonly struct GraphicsMemoryGrant
{
    public bool HasLimit { get; init; }

    public ulong LimitBytes { get; init; }

    public ulong Version { get; init; }
}

internal struct VideoMemoryBudgetSnapshot
{
    public ulong BudgetBytes;

    public ulong CurrentUsageBytes;

    public ulong AvailableForReservationBytes;

    public ulong CurrentReservationBytes;
}

internal struct SegmentMemoryAccounting
{
    public ulong OwnedBytes;

    public ulong ReservationBytes;

    public ulong RetiredPendingBytes;

    public bool DxgiInitialized;

    public VideoMemoryBudgetSnapshot LastDxgiObservation;
}

internal struct DeviceMemoryObservationState
{
    public SegmentMemoryAccounting Local;

    public SegmentMemoryAccounting NonLocal;
}

internal struct SegmentPolicySnapshot
{
    public bool TopologyActive;

    public MemoryBudgetStatus DxgiStatus;

    public VideoMemoryBudgetSnapshot Dxgi;

    public bool BrokerConfigured;

    public BrokerGrantStatus GrantStatus;

    public GraphicsMemoryGrant Grant;

    public ulong? ExplicitHardLimitBytes;
}

internal readonly struct MemoryAdmissionSnapshot(
    ulong epoch,
    SegmentPolicySnapshot local,
    SegmentPolicySnapshot nonLocal,
    DeviceStructuralAggregate structural)
{
    public ulong Epoch { get; } = epoch;

    public SegmentPolicySnapshot Local { get; } = local;

    public SegmentPolicySnapshot NonLocal { get; } = nonLocal;

    public DeviceStructuralAggregate Structural { get; } = structural;

    public SegmentPolicySnapshot GetSegment(MemoryPlacement placement)
    {
        return placement is MemoryPlacement.Local ? Local : NonLocal;
    }
}
