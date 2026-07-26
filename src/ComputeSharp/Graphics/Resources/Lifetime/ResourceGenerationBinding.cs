using System;
using System.Diagnostics.CodeAnalysis;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;

namespace ComputeSharp.Resources.Lifetime;

internal interface IGenerationBoundResource
{
    void BindGeneration(IResourceGenerationOwner owner, int resourceIndex);

    bool TryGetGenerationBinding(out ResourceUsageBinding binding);
}

internal readonly struct ResourceUsageBinding(
    ResourceGenerationSetHandle set,
    uint resourceIndex,
    ResourceGenerationId generation,
    ComputeResourceAccess access,
    TrackedResourceState residentState)
{
    public ResourceGenerationSetHandle Set { get; } = set;

    public uint ResourceIndex { get; } = resourceIndex;

    public ResourceGenerationId Generation { get; } = generation;

    public ComputeResourceAccess Access { get; } = access;

    public TrackedResourceState ResidentState { get; } = residentState;
}

internal struct ResourceGenerationBinding
{
    private IResourceGenerationOwner? owner;

    private int resourceIndex;

    private ResourceGenerationSetId setId;

    private ComputeResourceAccess access;

    private TrackedResourceState residentState;

    private ResourceGenerationRecord record;

    public readonly ResourceGenerationSetId SetId => this.setId;

    [UnscopedRef]
    public ref ResourceGenerationRecord Record => ref this.record;

    public void InitializeUsage(ComputeResourceAccess access, TrackedResourceState residentState)
    {
        default(ArgumentException).ThrowIf(residentState is TrackedResourceState.Unknown, nameof(residentState));

        this.access = access;
        this.residentState = residentState;
    }

    public void InitializeSelfOwned(
        IResourceGenerationOwner self,
        ResourceIdentityAllocator identities,
        MemoryPlacement placement,
        ulong reclaimableBytes)
    {
        default(InvalidOperationException).ThrowIf(
            this.residentState is TrackedResourceState.Unknown,
            "The resource has no observed usage to own a generation with.");

        this.setId = identities.CreateGenerationSetId();
        this.record.ResourceId = identities.CreateResourceId();
        this.record.Id = identities.CreateGenerationId();
        this.record.Lifecycle = ResourceGenerationState.Active;
        this.record.D3D12State = this.residentState;
        this.record.Placement = placement;
        this.record.ReclaimableBytes = reclaimableBytes;
        this.record.ExternalObjectsReleased = 1;
        this.owner = self;
        this.resourceIndex = 0;
    }

    public void BindToOwner(IResourceGenerationOwner owner, int resourceIndex)
    {
        this.owner = owner;
        this.resourceIndex = resourceIndex;
    }

    public readonly bool TryGetBinding(out ResourceUsageBinding binding)
    {
        if (this.owner is not IResourceGenerationOwner owner ||
            this.residentState is TrackedResourceState.Unknown)
        {
            binding = default;

            return false;
        }

        binding = new ResourceUsageBinding(
            new ResourceGenerationSetHandle(owner),
            (uint)this.resourceIndex,
            owner.GetResourceRecord(this.resourceIndex).Id,
            this.access,
            this.residentState);

        return true;
    }
}
