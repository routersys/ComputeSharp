using System;

namespace ComputeSharp.Graphics.Pipelines;

internal static class ResourcePlanEvaluator
{
    public static void ValidatePlan(in OwnedSlotDescriptor slot, ReadOnlySpan<int> plan, string parameterName)
    {
        default(ArgumentException).ThrowIf(plan.Length != slot.PlanFields.Length, parameterName);

        for (int i = 0; i < plan.Length; i++)
        {
            default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(plan[i], parameterName);
        }
    }

    public static ResourcePlanDecision Evaluate(in OwnedSlotDescriptor slot, ReadOnlySpan<int> requestedPlan, ReadOnlySpan<int> activePlan)
    {
        int fieldCount = slot.PlanFields.Length;

        default(ArgumentException).ThrowIf(requestedPlan.Length != fieldCount, nameof(requestedPlan));
        default(ArgumentException).ThrowIf(activePlan.Length != fieldCount, nameof(activePlan));

        for (int i = 0; i < fieldCount; i++)
        {
            if (requestedPlan[i] != activePlan[i])
            {
                return ResourcePlanDecision.Replacement;
            }
        }

        return ResourcePlanDecision.Identical;
    }

    public static void ValidateSharedTexturePlan(int width, int height)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(width);
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(height);
    }

    public static ResourcePlanDecision EvaluateSharedTexture(
        in SharedTextureContractDescriptor sharedTexture,
        int requestedWidth,
        int requestedHeight,
        int activeWidth,
        int activeHeight,
        int physicalWidth,
        int physicalHeight)
    {
        if (requestedWidth == activeWidth && requestedHeight == activeHeight)
        {
            return ResourcePlanDecision.Identical;
        }

        if (sharedTexture.ResizePolicy is ComputeResourceResizePolicy.GrowOnly &&
            physicalWidth >= requestedWidth &&
            physicalHeight >= requestedHeight)
        {
            return ResourcePlanDecision.LogicalUpdate;
        }

        return ResourcePlanDecision.Replacement;
    }
}
