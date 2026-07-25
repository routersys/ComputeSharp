using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the final immutable contract model of a resource group.
/// </summary>
internal static class ResourceGroupContractModelBuilder
{
    /// <summary>
    /// Tries to build the final contract model of a given resource group.
    /// </summary>
    /// <param name="groupSymbol">The resource group type to build the contract model of.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="group">The resulting contract model, if every declaration is valid.</param>
    /// <returns>Whether the contract model of <paramref name="groupSymbol"/> could be built.</returns>
    public static bool TryBuild(
        INamedTypeSymbol groupSymbol,
        PipelineWellKnownSymbols symbols,
        out ResourceGroupContractInfo group)
    {
        group = null!;

        if (groupSymbol.IsGenericType ||
            !groupSymbol.HasAttributeWithType(symbols.ResourceGroupAttribute) ||
            !TryCollectMembers(groupSymbol, symbols, out IPropertySymbol[]? memberSymbols))
        {
            return false;
        }

        using ImmutableArrayBuilder<ResourceGroupMemberContractInfo> memberBuilder = new();
        using ImmutableArrayBuilder<ResourcePlanFieldContractInfo> planFieldBuilder = new();

        HashSet<string> canonicalNames = [];

        for (int i = 0; i < memberSymbols.Length; i++)
        {
            IPropertySymbol memberSymbol = memberSymbols[i];

            if (!memberSymbol.TryGetAttributeWithType(symbols.PipelineResourceAttribute, out AttributeData? memberAttribute) ||
                !PipelineResourceContractReader.TryRead(memberAttribute, out ComputeResourceAccess memberAccess, out bool memberHasRecovery, out _) ||
                memberHasRecovery ||
                !GeneratedIdentifier.TryCreateCanonicalName(memberSymbol.MetadataName, out string canonicalName) ||
                !canonicalNames.Add(canonicalName) ||
                !ResourcePlanGrammar.TryAppendPlanFields(memberSymbol.Type, memberSymbol.MetadataName, (uint)i, in planFieldBuilder))
            {
                return false;
            }

            memberBuilder.Add(new ResourceGroupMemberContractInfo(
                (uint)i,
                memberSymbol.MetadataName,
                CanonicalTypeNameBuilder.GetCanonicalTypeName(memberSymbol.Type),
                memberAccess));
        }

        group = new ResourceGroupContractInfo(
            CanonicalTypeNameBuilder.GetCanonicalTypeName(groupSymbol),
            memberBuilder.ToImmutable(),
            PipelineCanonicalOrdering.OrderPlanFields(planFieldBuilder.ToImmutable()));

        return true;
    }

    /// <summary>
    /// Tries to collect every annotated member of a given resource group, in canonical order.
    /// </summary>
    /// <param name="groupSymbol">The resource group type.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="memberSymbols">The resulting annotated members, in canonical order.</param>
    /// <returns>Whether every annotated member of <paramref name="groupSymbol"/> is a valid group member.</returns>
    private static bool TryCollectMembers(
        INamedTypeSymbol groupSymbol,
        PipelineWellKnownSymbols symbols,
        [NotNullWhen(true)] out IPropertySymbol[]? memberSymbols)
    {
        using ImmutableArrayBuilder<IPropertySymbol> builder = new();

        foreach (ISymbol memberSymbol in groupSymbol.GetMembers())
        {
            if (!memberSymbol.HasAttributeWithType(symbols.PipelineResourceAttribute))
            {
                continue;
            }

            if (memberSymbol is not IPropertySymbol { SetMethod: null, GetMethod: not null, IsIndexer: false, IsStatic: false } propertySymbol)
            {
                memberSymbols = null;

                return false;
            }

            builder.Add(propertySymbol);
        }

        if (builder.Count == 0)
        {
            memberSymbols = null;

            return false;
        }

        IPropertySymbol[] members = [.. builder.ToImmutable()];

        System.Array.Sort(members, static (left, right) => string.CompareOrdinal(left.MetadataName, right.MetadataName));

        memberSymbols = members;

        return true;
    }
}
