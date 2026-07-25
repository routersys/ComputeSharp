using System;
using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The canonical ordering rules for the members of a pipeline contract model.
/// </summary>
internal static class PipelineCanonicalOrdering
{
    /// <summary>
    /// Compares two host type metadata names in canonical order.
    /// </summary>
    /// <param name="left">The first host type metadata name.</param>
    /// <param name="right">The second host type metadata name.</param>
    /// <returns>The relative canonical order of the two names.</returns>
    public static int CompareHosts(string left, string right)
    {
        return string.CompareOrdinal(left, right);
    }

    /// <summary>
    /// Orders a set of pipelines in canonical order and assigns their ordinals.
    /// </summary>
    /// <param name="pipelines">The pipelines to order.</param>
    /// <param name="ordered">The resulting ordered pipelines, if no two of them share a canonical signature.</param>
    /// <returns>Whether the pipelines could be ordered.</returns>
    public static bool TryOrderPipelines(EquatableArray<PipelineContractInfo> pipelines, out EquatableArray<PipelineContractInfo> ordered)
    {
        PipelineContractInfo[] items = [.. pipelines.AsImmutableArray()];

        Array.Sort(items, static (left, right) => string.CompareOrdinal(left.CanonicalSignature, right.CanonicalSignature));

        for (int i = 1; i < items.Length; i++)
        {
            if (string.CompareOrdinal(items[i - 1].CanonicalSignature, items[i].CanonicalSignature) == 0)
            {
                ordered = default;

                return false;
            }
        }

        for (int i = 0; i < items.Length; i++)
        {
            items[i] = items[i] with { Ordinal = (uint)i };
        }

        ordered = ImmutableArray.Create(items);

        return true;
    }

    /// <summary>
    /// Orders a set of owned slots in canonical order and assigns their ordinals.
    /// </summary>
    /// <param name="slots">The owned slots to order.</param>
    /// <returns>The resulting ordered owned slots.</returns>
    public static EquatableArray<OwnedSlotContractInfo> OrderSlots(EquatableArray<OwnedSlotContractInfo> slots)
    {
        OwnedSlotContractInfo[] items = [.. slots.AsImmutableArray()];

        Array.Sort(items, static (left, right) => string.CompareOrdinal(left.MemberMetadataName, right.MemberMetadataName));

        for (int i = 0; i < items.Length; i++)
        {
            items[i] = items[i] with { Ordinal = (uint)i };
        }

        return ImmutableArray.Create(items);
    }

    /// <summary>
    /// Orders a set of shared textures in canonical order and assigns their ordinals.
    /// </summary>
    /// <param name="sharedTextures">The shared textures to order.</param>
    /// <returns>The resulting ordered shared textures.</returns>
    public static EquatableArray<SharedTextureContractInfo> OrderSharedTextures(EquatableArray<SharedTextureContractInfo> sharedTextures)
    {
        SharedTextureContractInfo[] items = [.. sharedTextures.AsImmutableArray()];

        Array.Sort(items, static (left, right) => string.CompareOrdinal(left.MemberMetadataName, right.MemberMetadataName));

        for (int i = 0; i < items.Length; i++)
        {
            items[i] = items[i] with { Ordinal = (uint)i };
        }

        return ImmutableArray.Create(items);
    }

    /// <summary>
    /// Orders a set of plan fields in canonical order and assigns their field ordinals.
    /// </summary>
    /// <param name="planFields">The plan fields to order, already in canonical member and dimension order.</param>
    /// <returns>The resulting ordered plan fields.</returns>
    public static EquatableArray<ResourcePlanFieldContractInfo> OrderPlanFields(EquatableArray<ResourcePlanFieldContractInfo> planFields)
    {
        ResourcePlanFieldContractInfo[] items = [.. planFields.AsImmutableArray()];

        Array.Sort(items, static (left, right) =>
        {
            int comparison = left.SlotResourceIndex.CompareTo(right.SlotResourceIndex);

            return comparison != 0 ? comparison : left.DimensionKind.CompareTo(right.DimensionKind);
        });

        for (int i = 0; i < items.Length; i++)
        {
            items[i] = items[i] with { FieldOrdinal = (uint)i };
        }

        return ImmutableArray.Create(items);
    }

    /// <summary>
    /// Assigns the contiguous resource ordinals of a pipeline, with parameters preceding internal resources.
    /// </summary>
    /// <param name="parameters">The resource contracts bound through parameters, in source parameter order.</param>
    /// <param name="internalResources">The resource contracts owned by the host, in canonical member order.</param>
    /// <param name="orderedParameters">The resulting parameters, with their ordinals assigned.</param>
    /// <param name="orderedInternalResources">The resulting internal resources, with their ordinals assigned.</param>
    public static void AssignResourceOrdinals(
        EquatableArray<ResourceContractInfo> parameters,
        EquatableArray<ResourceContractInfo> internalResources,
        out EquatableArray<ResourceContractInfo> orderedParameters,
        out EquatableArray<ResourceContractInfo> orderedInternalResources)
    {
        ResourceContractInfo[] parameterItems = [.. parameters.AsImmutableArray()];
        ResourceContractInfo[] internalItems = [.. internalResources.AsImmutableArray()];

        for (int i = 0; i < parameterItems.Length; i++)
        {
            parameterItems[i] = parameterItems[i] with { Ordinal = (uint)i };
        }

        for (int i = 0; i < internalItems.Length; i++)
        {
            internalItems[i] = internalItems[i] with { Ordinal = (uint)checked(parameterItems.Length + i) };
        }

        orderedParameters = ImmutableArray.Create(parameterItems);
        orderedInternalResources = ImmutableArray.Create(internalItems);
    }
}
