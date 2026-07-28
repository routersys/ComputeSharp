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

    public ResourceUsageBinding AsReadOnlyView()
    {
        return new ResourceUsageBinding(
            Set,
            ResourceIndex,
            Generation,
            ComputeResourceAccess.Read,
            TrackedResourceState.NonPixelShaderResource);
    }
}

internal struct ResourceGenerationBinding
{
    private IResourceGenerationOwner? owner;

    private int resourceIndex;

    private ResourceGenerationSetId setId;

    private ComputeResourceAccess access;

    private ResourceGenerationRecord record;

    public readonly ResourceGenerationSetId SetId => this.setId;

    [UnscopedRef]
    public ref ResourceGenerationRecord Record => ref this.record;

    public void InitializeObservedAccess(ComputeResourceAccess access)
    {
        this.access = access;
    }

    public void InitializeSelfOwned(
        IResourceGenerationOwner self,
        ResourceIdentityAllocator identities,
        TrackedResourceState d3D12State,
        MemoryPlacement placement,
        ulong reclaimableBytes)
    {
        default(ArgumentException).ThrowIf(d3D12State is TrackedResourceState.Unknown, nameof(d3D12State));

        this.setId = identities.CreateGenerationSetId();
        this.record.ResourceId = identities.CreateResourceId();
        this.record.Id = identities.CreateGenerationId();
        this.record.Lifecycle = ResourceGenerationState.Active;
        this.record.D3D12State = d3D12State;
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

    public readonly bool TryGetBinding(TrackedResourceState residentState, out ResourceUsageBinding binding)
    {
        default(ArgumentException).ThrowIf(residentState is TrackedResourceState.Unknown, nameof(residentState));

        if (this.owner is not IResourceGenerationOwner owner)
        {
            binding = default;

            return false;
        }

        binding = new ResourceUsageBinding(
            new ResourceGenerationSetHandle(owner),
            (uint)this.resourceIndex,
            owner.GetResourceRecord(this.resourceIndex).Id,
            this.access,
            residentState);

        return true;
    }
}
