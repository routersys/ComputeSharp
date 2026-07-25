using System.Collections.Generic;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the final immutable contract model of a compute pipeline host.
/// </summary>
internal static class PipelineHostContractModelBuilder
{
    /// <summary>
    /// Tries to build the final contract model of a given host.
    /// </summary>
    /// <param name="hostSymbol">The host type to build the contract model of.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="host">The resulting contract model, if every declaration is valid.</param>
    /// <returns>Whether the contract model of <paramref name="hostSymbol"/> could be built.</returns>
    public static bool TryBuild(
        INamedTypeSymbol hostSymbol,
        PipelineWellKnownSymbols symbols,
        out PipelineHostContractInfo host)
    {
        host = null!;

        if (!hostSymbol.TryGetAttributeWithType(symbols.PipelineHostAttribute, out AttributeData? hostAttribute) ||
            hostAttribute.ConstructorArguments is not [{ Value: string }, { Value: int maximumConcurrentInvocations }] ||
            maximumConcurrentInvocations < 1 ||
            !HostResourceCollector.TryCollect(hostSymbol, symbols, out EquatableArray<OwnedSlotContractInfo> slots, out EquatableArray<UnorderedInternalResourceContract> resources) ||
            !PipelineCollector.TryCollect(hostSymbol, symbols, out EquatableArray<PipelineContractInfo> pipelines) ||
            !TryResolveInternalResources(slots, resources, out EquatableArray<ResourceContractInfo> internalResources))
        {
            return false;
        }

        using ImmutableArrayBuilder<PipelineContractInfo> pipelineBuilder = new();

        foreach (PipelineContractInfo pipeline in pipelines)
        {
            PipelineCanonicalOrdering.AssignResourceOrdinals(
                pipeline.Parameters,
                internalResources,
                out EquatableArray<ResourceContractInfo> orderedParameters,
                out EquatableArray<ResourceContractInfo> orderedInternalResources);

            pipelineBuilder.Add(PipelineStructuralRequirements.Derive(pipeline with
            {
                Parameters = orderedParameters,
                InternalResources = orderedInternalResources
            }));
        }

        if (!PipelineCanonicalOrdering.TryOrderPipelines(pipelineBuilder.ToImmutable(), out EquatableArray<PipelineContractInfo> orderedPipelines))
        {
            return false;
        }

        host = new PipelineHostContractInfo(
            CanonicalTypeNameBuilder.GetCanonicalTypeName(hostSymbol),
            maximumConcurrentInvocations,
            PipelineStructuralRequirements.Derive(orderedPipelines, slots.Length),
            orderedPipelines,
            slots);

        return true;
    }

    /// <summary>
    /// Tries to resolve the slot ordinals of a set of collected internal resource contracts.
    /// </summary>
    /// <param name="slots">The owned slots of the host, in canonical order.</param>
    /// <param name="resources">The collected internal resource contracts, in canonical order.</param>
    /// <param name="internalResources">The resulting internal resource contracts, with their slot ordinals resolved.</param>
    /// <returns>Whether every slot reference could be resolved.</returns>
    private static bool TryResolveInternalResources(
        EquatableArray<OwnedSlotContractInfo> slots,
        EquatableArray<UnorderedInternalResourceContract> resources,
        out EquatableArray<ResourceContractInfo> internalResources)
    {
        Dictionary<string, uint> slotOrdinals = [];

        foreach (OwnedSlotContractInfo slot in slots)
        {
            slotOrdinals.Add(slot.MemberMetadataName, slot.Ordinal);
        }

        using ImmutableArrayBuilder<ResourceContractInfo> builder = new();

        foreach (UnorderedInternalResourceContract resource in resources)
        {
            uint slotOrdinal = 0;

            if (resource.SlotKey is { } slotKey && !slotOrdinals.TryGetValue(slotKey.HostMemberMetadataName, out slotOrdinal))
            {
                internalResources = default;

                return false;
            }

            builder.Add(new ResourceContractInfo(
                uint.MaxValue,
                resource.ResourceTypeMetadataName,
                resource.Access,
                resource.Sharing,
                resource.Aliasing,
                resource.Ownership,
                resource.SlotKey is not null,
                slotOrdinal,
                resource.SlotResourceIndex));
        }

        internalResources = builder.ToImmutable();

        return true;
    }
}
