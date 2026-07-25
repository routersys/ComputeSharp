using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal interface IResourceGenerationOwner
{
    ResourceGenerationSetId SetId { get; }

    int ResourceCount { get; }

    ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal);
}

internal readonly struct ResourceGenerationSetHandle
{
    public ResourceGenerationSetHandle(IResourceGenerationOwner owner)
    {
        default(ArgumentNullException).ThrowIfNull(owner);

        if (owner.SetId.Value == 0 || owner.ResourceCount <= 0)
        {
            default(ArgumentException).Throw(nameof(owner), "The generation owner is invalid.");
        }

        Owner = owner;
        SetId = owner.SetId;
    }

    public ResourceGenerationSetId SetId { get; }

    public IResourceGenerationOwner Owner { get; }

    public bool IsEmpty => Owner is null;
}

internal readonly struct ResourceGenerationPin(ResourceGenerationSetHandle handle, ResourceGenerationId generationId, int resourceIndex)
{
    public ResourceGenerationSetHandle Handle { get; } = handle;

    public ResourceGenerationId GenerationId { get; } = generationId;

    public int ResourceIndex { get; } = resourceIndex;
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
