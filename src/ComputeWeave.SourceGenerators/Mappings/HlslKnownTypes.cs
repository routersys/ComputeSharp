using System;
using System.Collections.Generic;
using System.Linq;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
partial class HlslKnownTypes
{
    /// <summary>
    /// Gets the known HLSL dispatch types.
    /// </summary>
    public static IReadOnlyCollection<Type> HlslDispatchTypes { get; } =
    [
        typeof(ThreadIds),
        typeof(GroupIds),
        typeof(GridIds)
    ];

    /// <summary>
    /// Checks whether or not a given type name matches a constant buffer type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a constant buffer type.</returns>
    public static bool IsConstantBufferType(string typeName)
    {
        return typeName == "ComputeWeave.ConstantBuffer`1";
    }

    /// <summary>
    /// Checks whether or not a given type name matches a read write buffer type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a read write buffer type.</returns>
    public static bool IsReadWriteBufferType(string typeName)
    {
        return typeName == "ComputeWeave.ReadWriteBuffer`1";
    }

    /// <summary>
    /// Checks whether or not a given type name matches a structured buffer type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a structured buffer type.</returns>
    public static bool IsStructuredBufferType(string typeName)
    {
        return typeName switch
        {
            "ComputeWeave.ConstantBuffer`1" or
            "ComputeWeave.ReadOnlyBuffer`1" or
            "ComputeWeave.ReadWriteBuffer`1" or
            "ComputeWeave.IReadOnlyBuffer`1" => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether or not a given type name matches a readonly typed resource type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a readonly typed resource type.</returns>
    public static bool IsReadOnlyTypedResourceType(string typeName)
    {
        return typeName switch
        {
            "ComputeWeave.ReadOnlyBuffer`1" or
            "ComputeWeave.IReadOnlyBuffer`1" or
            "ComputeWeave.ReadOnlyTexture1D`1" or
            "ComputeWeave.ReadOnlyTexture1D`2" or
            "ComputeWeave.ReadOnlyTexture2D`1" or
            "ComputeWeave.ReadOnlyTexture2D`2" or
            "ComputeWeave.ReadOnlyTexture3D`1" or
            "ComputeWeave.ReadOnlyTexture3D`2" or
            "ComputeWeave.IReadOnlyTexture1D`1" or
            "ComputeWeave.IReadOnlyTexture2D`1" or
            "ComputeWeave.IReadOnlyTexture3D`1" => true,
            "ComputeWeave.IReadOnlyNormalizedTexture1D`1" or
            "ComputeWeave.IReadOnlyNormalizedTexture2D`1" or
            "ComputeWeave.IReadOnlyNormalizedTexture3D`1" => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether or not a given type name matches a writeable typed resource type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a writeable typed resource type.</returns>
    public static bool IsReadWriteTypedResourceType(string typeName)
    {
        return typeName switch
        {
            "ComputeWeave.ReadWriteBuffer`1" or
            "ComputeWeave.ReadWriteTexture1D`1" or
            "ComputeWeave.ReadWriteTexture1D`2" or
            "ComputeWeave.ReadWriteTexture2D`1" or
            "ComputeWeave.ReadWriteTexture2D`2" or
            "ComputeWeave.ReadWriteTexture3D`1" or
            "ComputeWeave.ReadWriteTexture3D`2" or
            "ComputeWeave.IReadWriteNormalizedTexture1D`1" or
            "ComputeWeave.IReadWriteNormalizedTexture2D`1" or
            "ComputeWeave.IReadWriteNormalizedTexture3D`1" => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether or not a given type name matches a typed resource type.
    /// </summary>
    /// <param name="typeName">The input type name to check.</param>
    /// <returns>Whether or not <paramref name="typeName"/> represents a typed resource type.</returns>
    public static bool IsTypedResourceType(string typeName)
    {
        return typeName switch
        {
            "ComputeWeave.ConstantBuffer`1" or
            "ComputeWeave.ReadOnlyBuffer`1" or
            "ComputeWeave.ReadWriteBuffer`1" or
            "ComputeWeave.IReadOnlyBuffer`1" or
            "ComputeWeave.ReadOnlyTexture1D`1" or
            "ComputeWeave.ReadOnlyTexture1D`2" or
            "ComputeWeave.ReadOnlyTexture2D`1" or
            "ComputeWeave.ReadOnlyTexture2D`2" or
            "ComputeWeave.ReadWriteTexture1D`1" or
            "ComputeWeave.ReadWriteTexture1D`2" or
            "ComputeWeave.ReadWriteTexture2D`1" or
            "ComputeWeave.ReadWriteTexture2D`2" or
            "ComputeWeave.ReadOnlyTexture3D`1" or
            "ComputeWeave.ReadOnlyTexture3D`2" or
            "ComputeWeave.ReadWriteTexture3D`1" or
            "ComputeWeave.ReadWriteTexture3D`2" or
            "ComputeWeave.IReadOnlyTexture1D`1" or
            "ComputeWeave.IReadOnlyTexture2D`1" or
            "ComputeWeave.IReadOnlyTexture3D`1" or
            "ComputeWeave.IReadOnlyNormalizedTexture1D`1" or
            "ComputeWeave.IReadWriteNormalizedTexture1D`1" or
            "ComputeWeave.IReadOnlyNormalizedTexture2D`1" or
            "ComputeWeave.IReadWriteNormalizedTexture2D`1" or
            "ComputeWeave.IReadOnlyNormalizedTexture3D`1" or
            "ComputeWeave.IReadWriteNormalizedTexture3D`1" => true,
            _ => false
        };
    }

    /// <inheritdoc/>
    public static partial bool IsKnownIndexableType(string typeName)
    {
        return IsKnownHlslType(typeName) || IsTypedResourceType(typeName);
    }

    /// <inheritdoc/>
    public static partial string GetMappedName(INamedTypeSymbol typeSymbol)
    {
        // Delegate types just return an empty string, as they're not actually
        // used in the generated shaders, but just mapped to a function at runtime.
        if (typeSymbol.TypeKind == TypeKind.Delegate)
        {
            return "";
        }

        string typeName = typeSymbol.GetFullyQualifiedMetadataName();

        // Special case for the resource types
        if (IsTypedResourceType(typeName))
        {
            string genericArgumentName = ((INamedTypeSymbol)typeSymbol.TypeArguments.Last()).GetFullyQualifiedMetadataName();

            // If the current type is a custom type, format it as needed
            if (!KnownHlslTypeMetadataNames.TryGetValue(genericArgumentName, out string? mappedElementType))
            {
                mappedElementType = genericArgumentName.ToHlslIdentifierName();
            }

            // Construct the HLSL type name
            return typeName switch
            {
                "ComputeWeave.ConstantBuffer`1" => mappedElementType,
                "ComputeWeave.ReadOnlyBuffer`1" => $"StructuredBuffer<{mappedElementType}>",
                "ComputeWeave.IReadOnlyBuffer`1" => $"StructuredBuffer<{mappedElementType}>",
                "ComputeWeave.ReadWriteBuffer`1" => $"RWStructuredBuffer<{mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture1D`1" => $"Texture1D<{mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture1D`2" => $"Texture1D<unorm {mappedElementType}>",
                "ComputeWeave.ReadWriteTexture1D`1" => $"RWTexture1D<{mappedElementType}>",
                "ComputeWeave.ReadWriteTexture1D`2" => $"RWTexture1D<unorm {mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture2D`1" => $"Texture2D<{mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture2D`2" => $"Texture2D<unorm {mappedElementType}>",
                "ComputeWeave.ReadWriteTexture2D`1" => $"RWTexture2D<{mappedElementType}>",
                "ComputeWeave.ReadWriteTexture2D`2" => $"RWTexture2D<unorm {mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture3D`1" => $"Texture3D<{mappedElementType}>",
                "ComputeWeave.ReadOnlyTexture3D`2" => $"Texture3D<unorm {mappedElementType}>",
                "ComputeWeave.ReadWriteTexture3D`1" => $"RWTexture3D<{mappedElementType}>",
                "ComputeWeave.ReadWriteTexture3D`2" => $"RWTexture3D<unorm {mappedElementType}>",
                "ComputeWeave.IReadOnlyTexture1D`1" => $"Texture1D<{mappedElementType}>",
                "ComputeWeave.IReadOnlyTexture2D`1" => $"Texture2D<{mappedElementType}>",
                "ComputeWeave.IReadOnlyTexture3D`1" => $"Texture3D<{mappedElementType}>",
                "ComputeWeave.IReadOnlyNormalizedTexture1D`1" => $"Texture1D<unorm {mappedElementType}>",
                "ComputeWeave.IReadWriteNormalizedTexture1D`1" => $"RWTexture1D<unorm {mappedElementType}>",
                "ComputeWeave.IReadOnlyNormalizedTexture2D`1" => $"Texture2D<unorm {mappedElementType}>",
                "ComputeWeave.IReadWriteNormalizedTexture2D`1" => $"RWTexture2D<unorm {mappedElementType}>",
                "ComputeWeave.IReadOnlyNormalizedTexture3D`1" => $"Texture3D<unorm {mappedElementType}>",
                "ComputeWeave.IReadWriteNormalizedTexture3D`1" => $"RWTexture3D<unorm {mappedElementType}>",
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

    /// <summary>
    /// Gets the mapped HLSL-compatible type name for the output texture of a pixel shader.
    /// </summary>
    /// <param name="typeSymbol">The shader type to map.</param>
    /// <returns>The HLSL-compatible type name that can be used in an HLSL shader.</returns>
    public static string? GetMappedNameForPixelShaderType(INamedTypeSymbol typeSymbol)
    {
        // If the shader type is not a pixel shader type (ie. it has a type argument), stop here.
        // At this point the input is guaranteed to either be 'IComputeShader' or 'IComputeShader<TPixel>'.
        if (typeSymbol.TypeArguments is not [INamedTypeSymbol pixelShaderType])
        {
            return null;
        }

        string genericArgumentName = pixelShaderType.GetFullyQualifiedMetadataName();

        // If the current type is a custom type, format it as needed
        if (!KnownHlslTypeMetadataNames.TryGetValue(genericArgumentName, out string? mappedElementType))
        {
            mappedElementType = genericArgumentName.ToHlslIdentifierName();
        }

        // Construct the HLSL type name
        return $"RWTexture2D<unorm {mappedElementType}>";
    }
}