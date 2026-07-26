namespace ComputeSharp.Memory;

internal struct BrokerGrantObservation
{
    public bool Initialized;

    public GraphicsMemoryGrant Grant;
}

internal sealed class MemoryPolicyConfiguration
{
    public ulong ConfigurationVersion;

    public MemoryPolicyConfigurationState State;

    public IGraphicsMemoryBudgetClient? BrokerClient;

    public ulong? LocalOwnedHardLimitBytes;

    public ulong? NonLocalOwnedHardLimitBytes;

    public BrokerGrantObservation LocalGrantObservation;

    public BrokerGrantObservation NonLocalGrantObservation;

    public int LeaseCount;

    public ulong? GetExplicitHardLimitBytes(MemoryPlacement placement)
    {
        return placement is MemoryPlacement.Local ? this.LocalOwnedHardLimitBytes : this.NonLocalOwnedHardLimitBytes;
    }

    public ref BrokerGrantObservation GetGrantObservation(MemoryPlacement placement)
    {
        return ref placement is MemoryPlacement.Local ? ref this.LocalGrantObservation : ref this.NonLocalGrantObservation;
    }
}

internal struct MemoryPolicyState
{
    public ulong NextConfigurationVersion;

    public ulong Epoch;

    public MemoryPolicyConfiguration Active;

    public MemoryPolicyConfiguration? Retired;
}
