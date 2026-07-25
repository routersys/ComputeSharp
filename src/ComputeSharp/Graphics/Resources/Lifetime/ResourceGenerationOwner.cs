using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal interface IResourceGenerationOwner
{
    ResourceGenerationSetId SetId { get; }

    int ResourceCount { get; }

    ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal);
}

internal readonly struct ResourceGenerationSetHandle(ResourceGenerationSetId setId, IResourceGenerationOwner owner)
{
    public ResourceGenerationSetId SetId { get; } = setId;

    public IResourceGenerationOwner Owner { get; } = owner;

    public bool IsEmpty => Owner is null;
}

internal readonly struct ResourceBindingRecord(
    SlotOrdinal slot,
    ResourceGenerationSetId setId,
    ResourceGenerationId generationId,
    ulong bindingEpoch,
    int resourceIndex)
{
    public SlotOrdinal Slot { get; } = slot;

    public ResourceGenerationSetId SetId { get; } = setId;

    public ResourceGenerationId GenerationId { get; } = generationId;

    public ulong BindingEpoch { get; } = bindingEpoch;

    public int ResourceIndex { get; } = resourceIndex;
}
