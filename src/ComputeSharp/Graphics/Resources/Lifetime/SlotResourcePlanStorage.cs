using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal static class SlotResourcePlanStorage
{
    private const int RegionCount = 3;

    private const int SharedTextureFieldCount = 2;

    public static int CreateHostPlanStates(in PipelineHostDescriptor host, Span<SlotResourcePlanStateRecord> states)
    {
        ReadOnlySpan<OwnedSlotDescriptor> slots = host.Slots.Span;

        default(ArgumentException).ThrowIf(states.Length != slots.Length, nameof(states));

        int storageOffset = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            int fieldCount = slots[i].PlanFields.Length;

            states[i] = new SlotResourcePlanStateRecord(storageOffset, fieldCount);

            storageOffset = checked(storageOffset + (RegionCount * fieldCount));
        }

        return storageOffset;
    }

    public static int CreateResourceSetPlanStates(in InteropResourceSetDescriptor resourceSet, Span<SlotResourcePlanStateRecord> states)
    {
        int slotCount = resourceSet.SharedTextures.Length;

        default(ArgumentException).ThrowIf(states.Length != slotCount, nameof(states));

        int storageOffset = 0;

        for (int i = 0; i < slotCount; i++)
        {
            states[i] = new SlotResourcePlanStateRecord(storageOffset, SharedTextureFieldCount);

            storageOffset = checked(storageOffset + (RegionCount * SharedTextureFieldCount));
        }

        return storageOffset;
    }

    public static Span<int> GetActiveLogicalPlan(int[] storage, in SlotResourcePlanStateRecord state)
    {
        return storage.AsSpan(state.StorageOffset, state.FieldCount);
    }

    public static Span<int> GetActivePhysicalCapacity(int[] storage, in SlotResourcePlanStateRecord state)
    {
        return storage.AsSpan(checked(state.StorageOffset + state.FieldCount), state.FieldCount);
    }

    public static Span<int> GetPreparedPlan(int[] storage, in SlotResourcePlanStateRecord state)
    {
        return storage.AsSpan(checked(state.StorageOffset + (state.FieldCount * 2)), state.FieldCount);
    }

    public static void ClearSlot(int[] storage, in SlotResourcePlanStateRecord state)
    {
        storage.AsSpan(state.StorageOffset, checked(RegionCount * state.FieldCount)).Clear();
    }
}
