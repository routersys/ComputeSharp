using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The compile-time compatibility rules between a graphics resource type and its declared compute access.
/// </summary>
internal static class ComputeAccessContract
{
    /// <summary>
    /// The fully qualified metadata names of the resource types bound to a shader as read-write.
    /// </summary>
    private static readonly string[] ReadWriteResourceTypeMetadataNames =
    [
        "ComputeSharp.ReadWriteBuffer`1",
        "ComputeSharp.ReadWriteTexture1D`1",
        "ComputeSharp.ReadWriteTexture1D`2",
        "ComputeSharp.ReadWriteTexture2D`1",
        "ComputeSharp.ReadWriteTexture2D`2",
        "ComputeSharp.ReadWriteTexture3D`1",
        "ComputeSharp.ReadWriteTexture3D`2"
    ];

    /// <summary>
    /// Checks whether a given resource type is compatible with a declared compute access.
    /// </summary>
    /// <param name="resourceTypeSymbol">The graphics resource type.</param>
    /// <param name="access">The declared compute access.</param>
    /// <returns>Whether <paramref name="resourceTypeSymbol"/> is compatible with <paramref name="access"/>.</returns>
    public static bool IsCompatible(ITypeSymbol resourceTypeSymbol, ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.ReadWrite || !IsReadWriteResourceType(resourceTypeSymbol);
    }

    /// <summary>
    /// Checks whether a given resource type is bound to a shader as read-write.
    /// </summary>
    /// <param name="resourceTypeSymbol">The graphics resource type.</param>
    /// <returns>Whether <paramref name="resourceTypeSymbol"/> is bound to a shader as read-write.</returns>
    private static bool IsReadWriteResourceType(ITypeSymbol resourceTypeSymbol)
    {
        if (resourceTypeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return false;
        }

        foreach (string metadataName in ReadWriteResourceTypeMetadataNames)
        {
            if (namedTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName(metadataName))
            {
                return true;
            }
        }

        return false;
    }
}
