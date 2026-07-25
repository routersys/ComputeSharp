using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the exact resource plan types generated from a contract model.
/// </summary>
internal static class ResourcePlanModelBuilder
{
    /// <summary>
    /// The name of the plan type nested into a resource group.
    /// </summary>
    private const string ResourceGroupPlanTypeName = "Plan";

    /// <summary>
    /// Tries to build the exact resource plan types of the owned slots of a given host.
    /// </summary>
    /// <param name="host">The pipeline host contract model to build the plan types of.</param>
    /// <param name="plans">The resulting plan types, in canonical slot ordinal order.</param>
    /// <returns>Whether the plan types of <paramref name="host"/> could be built.</returns>
    public static bool TryBuildHostPlans(PipelineHostContractInfo host, out EquatableArray<ResourcePlanInfo> plans)
    {
        using ImmutableArrayBuilder<ResourcePlanInfo> builder = new();

        foreach (OwnedSlotContractInfo slot in host.Slots)
        {
            if (slot.PlanKind is ResourcePlanKind.ResourceGroup)
            {
                continue;
            }

            if (!GeneratedIdentifier.TryCreateCanonicalName(slot.MemberMetadataName, out string canonicalName) ||
                !TryBuildPlan(GeneratedIdentifier.CreatePlanTypeName(canonicalName), slot.PlanFields, out ResourcePlanInfo plan))
            {
                plans = default;

                return false;
            }

            builder.Add(plan);
        }

        plans = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Tries to build the exact resource plan type of a given resource group.
    /// </summary>
    /// <param name="group">The resource group contract model to build the plan type of.</param>
    /// <param name="plan">The resulting plan type.</param>
    /// <returns>Whether the plan type of <paramref name="group"/> could be built.</returns>
    public static bool TryBuildGroupPlan(ResourceGroupContractInfo group, out ResourcePlanInfo plan)
    {
        return TryBuildPlan(ResourceGroupPlanTypeName, group.PlanFields, out plan);
    }

    /// <summary>
    /// Tries to build a single exact resource plan type.
    /// </summary>
    /// <param name="typeName">The name of the plan type.</param>
    /// <param name="planFields">The plan fields, in canonical field ordinal order.</param>
    /// <param name="plan">The resulting plan type.</param>
    /// <returns>Whether the plan type could be built.</returns>
    private static bool TryBuildPlan(string typeName, EquatableArray<ResourcePlanFieldContractInfo> planFields, out ResourcePlanInfo plan)
    {
        using ImmutableArrayBuilder<ResourcePlanFieldInfo> builder = new();

        foreach (ResourcePlanFieldContractInfo planField in planFields)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(planField.MemberMetadataName, out string canonicalName))
            {
                plan = null!;

                return false;
            }

            builder.Add(new ResourcePlanFieldInfo(
                planField.PlanParameterName,
                GeneratedIdentifier.CreatePlanPropertyName(canonicalName, planField.DimensionKind)));
        }

        plan = new ResourcePlanInfo(typeName, builder.ToImmutable());

        return true;
    }
}
