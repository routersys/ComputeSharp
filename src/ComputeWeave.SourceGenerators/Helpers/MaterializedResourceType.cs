using System.Collections.Immutable;
using ComputeWeave.Graphics.Pipelines;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGenerators.Helpers;

/// <summary>
/// The resource types the runtime materializes for the access contract of an owned resource.
/// </summary>
internal static class MaterializedResourceType
{
    /// <summary>
    /// Tries to get the resource type the runtime materializes for a declared owned resource.
    /// </summary>
    /// <param name="compilation">The compilation to resolve the resource type from.</param>
    /// <param name="declaredTypeSymbol">The declared type of the owned resource.</param>
    /// <param name="access">The declared compute access of the owned resource.</param>
    /// <param name="materializedTypeSymbol">The resulting materialized resource type, if one is defined.</param>
    /// <returns>Whether a materialized resource type is defined for <paramref name="declaredTypeSymbol"/>.</returns>
    public static bool TryGet(
        Compilation compilation,
        ITypeSymbol declaredTypeSymbol,
        ComputeResourceAccess access,
        out INamedTypeSymbol materializedTypeSymbol)
    {
        materializedTypeSymbol = null!;

        if (!ResourcePlanGrammar.TryGetPlanKind(declaredTypeSymbol, out ResourcePlanKind planKind) ||
            declaredTypeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return false;
        }

        ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

        if (typeArguments.Length is not (1 or 2) ||
            (planKind is ResourcePlanKind.Buffer && typeArguments.Length is not 1))
        {
            return false;
        }

        if (compilation.GetTypeByMetadataName(GetMetadataName(planKind, typeArguments.Length, access)) is not { } definitionSymbol)
        {
            return false;
        }

        materializedTypeSymbol = definitionSymbol.Construct([.. typeArguments]);

        return true;
    }

    /// <summary>
    /// Checks whether a materialized resource type can be held by a declared resource type.
    /// </summary>
    /// <param name="materializedTypeSymbol">The materialized resource type.</param>
    /// <param name="declaredTypeSymbol">The declared resource type.</param>
    /// <returns>Whether <paramref name="declaredTypeSymbol"/> can hold <paramref name="materializedTypeSymbol"/>.</returns>
    public static bool IsHeldBy(INamedTypeSymbol materializedTypeSymbol, ITypeSymbol declaredTypeSymbol)
    {
        for (INamedTypeSymbol? currentSymbol = materializedTypeSymbol; currentSymbol is not null; currentSymbol = currentSymbol.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(currentSymbol, declaredTypeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the fully qualified metadata name of the resource type the runtime materializes.
    /// </summary>
    /// <param name="planKind">The plan kind of the owned resource.</param>
    /// <param name="typeArgumentCount">The number of type arguments of the declared resource type.</param>
    /// <param name="access">The declared compute access of the owned resource.</param>
    /// <returns>The fully qualified metadata name of the materialized resource type.</returns>
    private static string GetMetadataName(ResourcePlanKind planKind, int typeArgumentCount, ComputeResourceAccess access)
    {
        bool isReadOnly = access is ComputeResourceAccess.Read;

        if (planKind is ResourcePlanKind.Buffer)
        {
            return isReadOnly ? "ComputeWeave.ReadOnlyBuffer`1" : "ComputeWeave.ReadWriteBuffer`1";
        }

        if (typeArgumentCount is 1)
        {
            return isReadOnly ? "ComputeWeave.ReadOnlyTexture2D`1" : "ComputeWeave.ReadWriteTexture2D`1";
        }

        return isReadOnly ? "ComputeWeave.ReadOnlyTexture2D`2" : "ComputeWeave.ReadWriteTexture2D`2";
    }
}
