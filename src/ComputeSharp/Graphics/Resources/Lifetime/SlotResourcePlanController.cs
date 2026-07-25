using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal static class SlotResourcePlanController
{
    public static bool TryInstallPrepared(
        ref SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ResourceGenerationSetHandle prepared,
        ulong preparedToken,
        ReadOnlySpan<int> requestedPlan)
    {
        default(ArgumentException).ThrowIf(requestedPlan.Length != planState.FieldCount, nameof(requestedPlan));

        if (!slot.TryInstallPrepared(prepared, preparedToken))
        {
            return false;
        }

        requestedPlan.CopyTo(SlotResourcePlanStorage.GetPreparedPlan(storage, planState));

        return true;
    }

    public static bool TryCommitReplacement(
        ref SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        Span<int> preparedPlan = SlotResourcePlanStorage.GetPreparedPlan(storage, planState);

        if (!slot.TryCommitReplacement(expectedActiveSetId, expectedBindingEpoch, preparedToken, out detachedPrepared))
        {
            if (!detachedPrepared.IsEmpty)
            {
                preparedPlan.Clear();
            }

            return false;
        }

        preparedPlan.CopyTo(SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState));
        preparedPlan.CopyTo(SlotResourcePlanStorage.GetActivePhysicalCapacity(storage, planState));
        preparedPlan.Clear();

        return true;
    }

    public static bool TryAbortReplacement(
        ref SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        if (!slot.TryAbortReplacement(preparedToken, out detachedPrepared))
        {
            return false;
        }

        SlotResourcePlanStorage.GetPreparedPlan(storage, planState).Clear();

        return true;
    }

    public static bool TryApplyLogicalUpdate(
        ref SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ReadOnlySpan<int> requestedPlan)
    {
        default(ArgumentException).ThrowIf(requestedPlan.Length != planState.FieldCount, nameof(requestedPlan));

        if (!slot.CanApplyLogicalUpdate(expectedActiveSetId, expectedBindingEpoch))
        {
            return false;
        }

        Span<int> activePhysicalCapacity = SlotResourcePlanStorage.GetActivePhysicalCapacity(storage, planState);

        for (int i = 0; i < requestedPlan.Length; i++)
        {
            if (requestedPlan[i] > activePhysicalCapacity[i])
            {
                return false;
            }
        }

        requestedPlan.CopyTo(SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState));

        return true;
    }

    public static bool TryTrim(ref SlotControlRecord slot, int[] storage, in SlotResourcePlanStateRecord planState)
    {
        if (!slot.TryTrim())
        {
            return false;
        }

        SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState).Clear();
        SlotResourcePlanStorage.GetActivePhysicalCapacity(storage, planState).Clear();

        return true;
    }

    public static ResourceGenerationSetHandle RequestDispose(ref SlotControlRecord slot, int[] storage, in SlotResourcePlanStateRecord planState)
    {
        ResourceGenerationSetHandle detachedPrepared = slot.RequestDispose();

        if (!detachedPrepared.IsEmpty)
        {
            SlotResourcePlanStorage.GetPreparedPlan(storage, planState).Clear();
        }

        if (slot.State is SlotControlState.Disposed)
        {
            SlotResourcePlanStorage.ClearSlot(storage, planState);
        }

        return detachedPrepared;
    }

    public static bool TryClearRetired(
        ref SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        ResourceGenerationSetId expectedSetId)
    {
        if (!slot.TryClearRetired(expectedSetId))
        {
            return false;
        }

        if (slot.State is SlotControlState.Disposed)
        {
            SlotResourcePlanStorage.ClearSlot(storage, planState);
        }

        return true;
    }

    public static bool TryCompleteRetiringActive(ref SlotControlRecord slot, int[] storage, in SlotResourcePlanStateRecord planState)
    {
        if (!slot.TryCompleteRetiringActive())
        {
            return false;
        }

        SlotResourcePlanStorage.ClearSlot(storage, planState);

        return true;
    }

    public static ResourcePlanDecision Evaluate(
        in SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        in OwnedSlotDescriptor descriptor,
        ReadOnlySpan<int> requestedPlan)
    {
        if (slot.Active.IsEmpty)
        {
            return ResourcePlanDecision.Replacement;
        }

        return ResourcePlanEvaluator.Evaluate(
            descriptor,
            requestedPlan,
            SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState));
    }

    public static ResourcePlanDecision EvaluateSharedTexture(
        in SlotControlRecord slot,
        int[] storage,
        in SlotResourcePlanStateRecord planState,
        in SharedTextureContractDescriptor descriptor,
        int requestedWidth,
        int requestedHeight)
    {
        if (slot.Active.IsEmpty)
        {
            return ResourcePlanDecision.Replacement;
        }

        Span<int> activeLogicalPlan = SlotResourcePlanStorage.GetActiveLogicalPlan(storage, planState);
        Span<int> activePhysicalCapacity = SlotResourcePlanStorage.GetActivePhysicalCapacity(storage, planState);

        return ResourcePlanEvaluator.EvaluateSharedTexture(
            descriptor,
            requestedWidth,
            requestedHeight,
            activeLogicalPlan[0],
            activeLogicalPlan[1],
            activePhysicalCapacity[0],
            activePhysicalCapacity[1]);
    }
}
