using System.Diagnostics.CodeAnalysis;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;

namespace ComputeSharp.Resources.Lifetime;

internal interface IGenerationBoundResource
{
    void BindGeneration(IResourceGenerationOwner owner, int resourceIndex);

    bool TryGetGenerationBinding(out ResourceGenerationSetHandle set, out uint resourceIndex, out ResourceGenerationId generation);
}

internal struct ResourceGenerationBinding
{
    private IResourceGenerationOwner? owner;

    private int resourceIndex;

    private ResourceGenerationSetId setId;

    private ResourceGenerationRecord record;

    public readonly ResourceGenerationSetId SetId => this.setId;

    [UnscopedRef]
    public ref ResourceGenerationRecord Record => ref this.record;

    public void InitializeSelfOwned(
        IResourceGenerationOwner self,
        ResourceIdentityAllocator identities,
        TrackedResourceState d3D12State,
        MemoryPlacement placement,
        ulong reclaimableBytes)
    {
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

    public readonly bool TryGetBinding(
        out ResourceGenerationSetHandle set,
        out uint resourceIndex,
        out ResourceGenerationId generation)
    {
        if (this.owner is not IResourceGenerationOwner owner)
        {
            set = default;
            resourceIndex = 0;
            generation = default;

            return false;
        }

        set = new ResourceGenerationSetHandle(owner);
        resourceIndex = (uint)this.resourceIndex;
        generation = owner.GetResourceRecord(this.resourceIndex).Id;

        return true;
    }
}
