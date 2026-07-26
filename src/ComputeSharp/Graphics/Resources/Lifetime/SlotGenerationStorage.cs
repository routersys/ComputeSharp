using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal readonly struct OwnedSlotResourceLayout(int storageOffset, int resourceCount, bool hasAccessContracts)
{
    public int StorageOffset { get; } = storageOffset;

    public int ResourceCount { get; } = resourceCount;

    public bool HasAccessContracts { get; } = hasAccessContracts;
}

internal static class SlotGenerationStorage
{
    public static int CreateHostSlotLayouts(in PipelineHostDescriptor host, Span<OwnedSlotResourceLayout> layouts)
    {
        ReadOnlySpan<OwnedSlotDescriptor> slots = host.Slots.Span;

        default(ArgumentException).ThrowIf(layouts.Length != slots.Length, nameof(layouts));

        int storageOffset = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            int resourceCount = GetResourceCount(in slots[i]);

            layouts[i] = new OwnedSlotResourceLayout(storageOffset, resourceCount, hasAccessContracts: false);

            storageOffset = checked(storageOffset + resourceCount);
        }

        return storageOffset;
    }

    public static void ResolveResourceAccesses(
        in PipelineHostDescriptor host,
        Span<OwnedSlotResourceLayout> layouts,
        Span<ComputeResourceAccess> accesses)
    {
        ReadOnlySpan<OwnedSlotDescriptor> slots = host.Slots.Span;

        default(ArgumentException).ThrowIf(layouts.Length != slots.Length, nameof(layouts));

        for (int i = 0; i < slots.Length; i++)
        {
            OwnedSlotResourceLayout layout = layouts[i];

            bool hasAccessContracts = true;

            for (int j = 0; j < layout.ResourceCount; j++)
            {
                if (!TryResolveResourceAccess(in host, slots[i].Ordinal, (uint)j, out ComputeResourceAccess access))
                {
                    hasAccessContracts = false;

                    continue;
                }

                accesses[layout.StorageOffset + j] = access;
            }

            layouts[i] = new OwnedSlotResourceLayout(layout.StorageOffset, layout.ResourceCount, hasAccessContracts);
        }
    }

    private static bool TryResolveResourceAccess(
        in PipelineHostDescriptor host,
        SlotOrdinal slot,
        uint slotResourceIndex,
        out ComputeResourceAccess access)
    {
        ReadOnlySpan<PipelineDescriptor> pipelines = host.Pipelines.Span;

        access = default;

        bool isResolved = false;

        for (int i = 0; i < pipelines.Length; i++)
        {
            ReadOnlySpan<ResourceContractDescriptor> resources = pipelines[i].InternalResources.Span;

            for (int j = 0; j < resources.Length; j++)
            {
                ref readonly ResourceContractDescriptor resource = ref resources[j];

                if (!resource.HasSlot || resource.Slot != slot || resource.SlotResourceIndex != slotResourceIndex)
                {
                    continue;
                }

                if (isResolved && resource.Access != access)
                {
                    return false;
                }

                access = resource.Access;
                isResolved = true;
            }
        }

        return isResolved;
    }

    private static int GetResourceCount(in OwnedSlotDescriptor slot)
    {
        ReadOnlySpan<ResourcePlanFieldDescriptor> planFields = slot.PlanFields.Span;

        uint resourceCount = 0;

        for (int i = 0; i < planFields.Length; i++)
        {
            resourceCount = Math.Max(resourceCount, planFields[i].SlotResourceIndex + 1);
        }

        return checked((int)resourceCount);
    }
}
