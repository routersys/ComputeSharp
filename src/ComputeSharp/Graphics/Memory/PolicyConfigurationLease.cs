namespace ComputeSharp.Memory;

internal ref struct PolicyConfigurationLease(MemoryAllocationCoordinator coordinator, MemoryPolicyConfiguration configuration)
{
    private MemoryAllocationCoordinator? coordinator = coordinator;

    public MemoryPolicyConfiguration Configuration { get; } = configuration;

    public void Dispose()
    {
        if (this.coordinator is not MemoryAllocationCoordinator owner)
        {
            return;
        }

        this.coordinator = null;

        owner.ReleaseConfigurationLease(Configuration);
    }
}
