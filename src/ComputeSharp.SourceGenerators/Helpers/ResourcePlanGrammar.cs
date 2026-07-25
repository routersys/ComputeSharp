using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The dimension grammar of the exact resource plans derived from owned resource types.
/// </summary>
internal static class ResourcePlanGrammar
{
    /// <summary>
    /// The fully qualified metadata name of the buffer base type.
    /// </summary>
    private const string BufferTypeMetadataName = "ComputeSharp.Resources.Buffer`1";

    /// <summary>
    /// The fully qualified metadata name of the 2D texture base type.
    /// </summary>
    private const string Texture2DTypeMetadataName = "ComputeSharp.Resources.Texture2D`1";

    /// <summary>
    /// Tries to get the plan kind of a given owned resource type.
    /// </summary>
    /// <param name="resourceTypeSymbol">The owned resource type.</param>
    /// <param name="planKind">The resulting plan kind, if the resource type is supported.</param>
    /// <returns>Whether <paramref name="resourceTypeSymbol"/> declares a supported plan kind.</returns>
    public static bool TryGetPlanKind(ITypeSymbol resourceTypeSymbol, out ResourcePlanKind planKind)
    {
        if (InheritsFromDefinition(resourceTypeSymbol, BufferTypeMetadataName))
        {
            planKind = ResourcePlanKind.Buffer;

            return true;
        }

        if (InheritsFromDefinition(resourceTypeSymbol, Texture2DTypeMetadataName))
        {
            planKind = ResourcePlanKind.Texture2D;

            return true;
        }

        planKind = default;

        return false;
    }

    /// <summary>
    /// Tries to append the plan fields of a given owned resource to a target builder.
    /// </summary>
    /// <param name="resourceTypeSymbol">The owned resource type.</param>
    /// <param name="memberMetadataName">The metadata name of the owning member.</param>
    /// <param name="slotResourceIndex">The index of the resource within its slot.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    /// <returns>Whether the plan fields of <paramref name="resourceTypeSymbol"/> could be appended.</returns>
    public static bool TryAppendPlanFields(
        ITypeSymbol resourceTypeSymbol,
        string memberMetadataName,
        uint slotResourceIndex,
        ref readonly ImmutableArrayBuilder<ResourcePlanFieldContractInfo> builder)
    {
        if (!TryGetPlanKind(resourceTypeSymbol, out ResourcePlanKind planKind) ||
            !GeneratedIdentifier.TryCreateCanonicalName(memberMetadataName, out string canonicalName))
        {
            return false;
        }

        string resourceTypeMetadataName = CanonicalTypeNameBuilder.GetCanonicalTypeName(resourceTypeSymbol);

        if (planKind is ResourcePlanKind.Buffer)
        {
            builder.Add(CreateField(memberMetadataName, resourceTypeMetadataName, canonicalName, slotResourceIndex, ResourcePlanDimensionKind.Length));

            return true;
        }

        builder.Add(CreateField(memberMetadataName, resourceTypeMetadataName, canonicalName, slotResourceIndex, ResourcePlanDimensionKind.Width));
        builder.Add(CreateField(memberMetadataName, resourceTypeMetadataName, canonicalName, slotResourceIndex, ResourcePlanDimensionKind.Height));

        return true;
    }

    /// <summary>
    /// Creates a single plan field for a given owned resource dimension.
    /// </summary>
    /// <param name="memberMetadataName">The metadata name of the owning member.</param>
    /// <param name="resourceTypeMetadataName">The canonical metadata name of the owned resource type.</param>
    /// <param name="canonicalName">The generated canonical name of the owning member.</param>
    /// <param name="slotResourceIndex">The index of the resource within its slot.</param>
    /// <param name="dimensionKind">The dimension the field carries.</param>
    /// <returns>The resulting plan field.</returns>
    private static ResourcePlanFieldContractInfo CreateField(
        string memberMetadataName,
        string resourceTypeMetadataName,
        string canonicalName,
        uint slotResourceIndex,
        ResourcePlanDimensionKind dimensionKind)
    {
        return new ResourcePlanFieldContractInfo(
            uint.MaxValue,
            slotResourceIndex,
            memberMetadataName,
            resourceTypeMetadataName,
            GeneratedIdentifier.CreatePlanParameterName(canonicalName, dimensionKind),
            dimensionKind);
    }

    /// <summary>
    /// Checks whether a given type inherits from a base type with a specified generic definition metadata name.
    /// </summary>
    /// <param name="typeSymbol">The type to check.</param>
    /// <param name="definitionMetadataName">The fully qualified metadata name of the generic base type definition.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> inherits from <paramref name="definitionMetadataName"/>.</returns>
    private static bool InheritsFromDefinition(ITypeSymbol typeSymbol, string definitionMetadataName)
    {
        for (INamedTypeSymbol? baseTypeSymbol = typeSymbol.BaseType; baseTypeSymbol is not null; baseTypeSymbol = baseTypeSymbol.BaseType)
        {
            if (baseTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName(definitionMetadataName))
            {
                return true;
            }
        }

        return false;
    }
}
