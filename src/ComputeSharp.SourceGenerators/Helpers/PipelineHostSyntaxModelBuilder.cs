using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the members generated for the owned slots of a compute pipeline host.
/// </summary>
internal static class PipelineHostSyntaxModelBuilder
{
    /// <summary>
    /// Tries to build the members generated for the owned slots of a given host.
    /// </summary>
    /// <param name="host">The pipeline host contract model to build the members of.</param>
    /// <param name="slots">The resulting owned slots, in canonical slot ordinal order.</param>
    /// <returns>Whether the members of every owned slot of <paramref name="host"/> could be built.</returns>
    public static bool TryBuild(PipelineHostContractInfo host, out EquatableArray<OwnedSlotSyntaxInfo> slots)
    {
        using ImmutableArrayBuilder<OwnedSlotSyntaxInfo> builder = new();

        foreach (OwnedSlotContractInfo slot in host.Slots)
        {
            if (!TryBuildSlot(slot, out OwnedSlotSyntaxInfo slotInfo))
            {
                slots = default;

                return false;
            }

            builder.Add(slotInfo);
        }

        slots = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Tries to build the members generated for a single owned slot.
    /// </summary>
    /// <param name="slot">The owned slot contract model to build the members of.</param>
    /// <param name="slotInfo">The resulting owned slot, if its members could be built.</param>
    /// <returns>Whether the members of <paramref name="slot"/> could be built.</returns>
    private static bool TryBuildSlot(OwnedSlotContractInfo slot, out OwnedSlotSyntaxInfo slotInfo)
    {
        slotInfo = null!;

        if (!GeneratedIdentifier.TryCreateCanonicalName(slot.MemberMetadataName, out string canonicalName) ||
            !TryBuildPlanFields(slot, out EquatableArray<ResourcePlanFieldInfo> planFields) ||
            !TryBuildResources(slot, out EquatableArray<SlotResourceSyntaxInfo> resources, out bool requiresDoublePrecisionSupport))
        {
            return false;
        }

        bool isResourceGroup = slot.PlanKind is ResourcePlanKind.ResourceGroup;

        slotInfo = new OwnedSlotSyntaxInfo(
            slot.Ordinal,
            canonicalName,
            slot.MemberMetadataName,
            isResourceGroup ? $"{slot.ResourceTypeName}.{GeneratedIdentifier.ResourceGroupPlanTypeName}" : GeneratedIdentifier.CreatePlanTypeName(canonicalName),
            GeneratedIdentifier.CreateMaterializerTypeName(canonicalName),
            isResourceGroup ? slot.ResourceTypeAccessibility : "public",
            slot.ResourceTypeAccessibility,
            isResourceGroup ? null : slot.ResourceTypeName,
            requiresDoublePrecisionSupport,
            planFields,
            resources);

        return true;
    }

    /// <summary>
    /// Tries to build the plan fields of a single owned slot.
    /// </summary>
    /// <param name="slot">The owned slot contract model to build the plan fields of.</param>
    /// <param name="planFields">The resulting plan fields, in canonical field ordinal order.</param>
    /// <returns>Whether the plan fields of <paramref name="slot"/> could be built.</returns>
    private static bool TryBuildPlanFields(OwnedSlotContractInfo slot, out EquatableArray<ResourcePlanFieldInfo> planFields)
    {
        using ImmutableArrayBuilder<ResourcePlanFieldInfo> builder = new();

        foreach (ResourcePlanFieldContractInfo planField in slot.PlanFields)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(planField.MemberMetadataName, out string canonicalName))
            {
                planFields = default;

                return false;
            }

            builder.Add(new ResourcePlanFieldInfo(
                planField.PlanParameterName,
                GeneratedIdentifier.CreatePlanPropertyName(canonicalName, planField.DimensionKind)));
        }

        planFields = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Tries to build the resource declarations of a single owned slot.
    /// </summary>
    /// <param name="slot">The owned slot contract model to build the declarations of.</param>
    /// <param name="resources">The resulting declarations, in slot resource index order.</param>
    /// <param name="requiresDoublePrecisionSupport">Whether any declaration stores double precision floating point numbers.</param>
    /// <returns>Whether the declarations of <paramref name="slot"/> could be built.</returns>
    private static bool TryBuildResources(
        OwnedSlotContractInfo slot,
        out EquatableArray<SlotResourceSyntaxInfo> resources,
        out bool requiresDoublePrecisionSupport)
    {
        using ImmutableArrayBuilder<SlotResourceSyntaxInfo> builder = new();

        requiresDoublePrecisionSupport = false;

        for (int i = 0; i < slot.Resources.Length; i++)
        {
            SlotResourceGenerationInfo resource = slot.Resources[i];

            if (resource.SlotResourceIndex != (uint)i ||
                !TryBuildDimensionParameterNames(slot, resource, out EquatableArray<string> dimensionParameterNames))
            {
                resources = default;
                requiresDoublePrecisionSupport = false;

                return false;
            }

            requiresDoublePrecisionSupport |= resource.RequiresDoublePrecisionSupport;

            builder.Add(new SlotResourceSyntaxInfo(
                resource.Shape,
                resource.ElementTypeName,
                resource.PixelTypeName,
                dimensionParameterNames));
        }

        resources = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Tries to build the plan parameters carrying the dimensions of a single owned resource.
    /// </summary>
    /// <param name="slot">The owned slot declaring the resource.</param>
    /// <param name="resource">The owned resource to build the dimension parameters of.</param>
    /// <param name="dimensionParameterNames">The resulting parameters, in declaration order.</param>
    /// <returns>Whether every dimension of <paramref name="resource"/> is carried by a plan field.</returns>
    private static bool TryBuildDimensionParameterNames(
        OwnedSlotContractInfo slot,
        SlotResourceGenerationInfo resource,
        out EquatableArray<string> dimensionParameterNames)
    {
        using ImmutableArrayBuilder<string> builder = new();

        foreach (ResourcePlanDimensionKind dimensionKind in GetDimensionKinds(resource.Shape))
        {
            if (!TryGetPlanParameterName(slot, resource.SlotResourceIndex, dimensionKind, out string planParameterName))
            {
                dimensionParameterNames = default;

                return false;
            }

            builder.Add(planParameterName);
        }

        dimensionParameterNames = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Gets the dimensions declared by a given resource shape, in declaration order.
    /// </summary>
    /// <param name="shape">The resource shape to get the dimensions of.</param>
    /// <returns>The dimensions declared by <paramref name="shape"/>.</returns>
    private static ResourcePlanDimensionKind[] GetDimensionKinds(ResourcePlanKind shape)
    {
        return shape is ResourcePlanKind.Buffer
            ? [ResourcePlanDimensionKind.Length]
            : [ResourcePlanDimensionKind.Width, ResourcePlanDimensionKind.Height];
    }

    /// <summary>
    /// Tries to get the plan parameter carrying a single dimension of an owned resource.
    /// </summary>
    /// <param name="slot">The owned slot declaring the resource.</param>
    /// <param name="slotResourceIndex">The index of the resource within <paramref name="slot"/>.</param>
    /// <param name="dimensionKind">The dimension to get the plan parameter of.</param>
    /// <param name="planParameterName">The resulting plan parameter name, if one carries the dimension.</param>
    /// <returns>Whether a plan field of <paramref name="slot"/> carries the requested dimension.</returns>
    private static bool TryGetPlanParameterName(
        OwnedSlotContractInfo slot,
        uint slotResourceIndex,
        ResourcePlanDimensionKind dimensionKind,
        out string planParameterName)
    {
        foreach (ResourcePlanFieldContractInfo planField in slot.PlanFields)
        {
            if (planField.SlotResourceIndex == slotResourceIndex && planField.DimensionKind == dimensionKind)
            {
                planParameterName = planField.PlanParameterName;

                return true;
            }
        }

        planParameterName = "";

        return false;
    }
}
