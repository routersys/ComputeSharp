using System;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
partial class HlslKnownTypes
{
    /// <summary>
    /// Checks whether or not a given type name matches a resource texture type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a readonly typed resource type.</returns>
    public static bool IsResourceTextureType(string typeName)
    {
        return typeName switch
        {
            "ComputeWeave.D2D1.D2D1ResourceTexture1D`1" or
            "ComputeWeave.D2D1.D2D1ResourceTexture2D`1" or
            "ComputeWeave.D2D1.D2D1ResourceTexture3D`1" => true,
            _ => false
        };
    }

    /// <inheritdoc/>
    public static partial string GetMappedName(INamedTypeSymbol typeSymbol)
    {
        string typeName = typeSymbol.GetFullyQualifiedMetadataName();

        // Special case for resource texture types
        if (IsResourceTextureType(typeName))
        {
            string genericArgumentName = ((INamedTypeSymbol)typeSymbol.TypeArguments[0]).GetFullyQualifiedMetadataName();

            // Get the HLSL name for the type argument (it can only be either float or float4)
            _ = KnownHlslTypeMetadataNames.TryGetValue(genericArgumentName, out string? mappedElementType);

            return typeName switch
            {
                "ComputeWeave.D2D1.D2D1ResourceTexture1D`1" => $"Texture1D<{mappedElementType}>",
                "ComputeWeave.D2D1.D2D1ResourceTexture2D`1" => $"Texture2D<{mappedElementType}>",
                "ComputeWeave.D2D1.D2D1ResourceTexture3D`1" => $"Texture3D<{mappedElementType}>",
                _ => throw new ArgumentException()
            };
        }

        // The captured field is of an HLSL primitive type
        if (KnownHlslTypeMetadataNames.TryGetValue(typeName, out string? mappedType))
        {
            return mappedType;
        }

        // The captured field is of a custom struct type
        return typeName.ToHlslIdentifierName();
    }
}