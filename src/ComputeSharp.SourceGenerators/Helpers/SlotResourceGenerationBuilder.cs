using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the materialization model of the resources owned by a slot.
/// </summary>
internal static class SlotResourceGenerationBuilder
{
    /// <summary>
    /// Tries to build the materialization model of a single owned resource.
    /// </summary>
    /// <param name="resourceTypeSymbol">The declared type of the owned resource.</param>
    /// <param name="slotResourceIndex">The index of the resource within its slot.</param>
    /// <param name="resource">The resulting materialization model, if the resource can be materialized.</param>
    /// <returns>Whether the materialization model of <paramref name="resourceTypeSymbol"/> could be built.</returns>
    public static bool TryBuild(ITypeSymbol resourceTypeSymbol, uint slotResourceIndex, out SlotResourceGenerationInfo resource)
    {
        resource = null!;

        if (!ResourcePlanGrammar.TryGetPlanKind(resourceTypeSymbol, out ResourcePlanKind shape) ||
            resourceTypeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return false;
        }

        ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

        if (typeArguments.Length is not (1 or 2) ||
            (shape is ResourcePlanKind.Buffer && typeArguments.Length is not 1))
        {
            return false;
        }

        HashSet<ITypeSymbol> visitedTypeSymbols = new(SymbolEqualityComparer.Default);

        resource = new SlotResourceGenerationInfo(
            slotResourceIndex,
            shape,
            typeArguments[0].GetFullyQualifiedName(includeGlobal: true),
            typeArguments.Length is 2 ? typeArguments[1].GetFullyQualifiedName(includeGlobal: true) : null,
            ContainsDoublePrecision(typeArguments[0], visitedTypeSymbols));

        return true;
    }

    /// <summary>
    /// Checks whether a given type is a double precision floating point number, or stores one in any of its fields.
    /// </summary>
    /// <param name="typeSymbol">The type to check.</param>
    /// <param name="visitedTypeSymbols">The types that have already been checked.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> is or stores a double precision floating point number.</returns>
    private static bool ContainsDoublePrecision(ITypeSymbol typeSymbol, HashSet<ITypeSymbol> visitedTypeSymbols)
    {
        if (typeSymbol.SpecialType is SpecialType.System_Double)
        {
            return true;
        }

        if (IsPrimitive(typeSymbol) || !visitedTypeSymbols.Add(typeSymbol))
        {
            return false;
        }

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
        {
            if (memberSymbol is IFieldSymbol { IsStatic: false } fieldSymbol &&
                ContainsDoublePrecision(fieldSymbol.Type, visitedTypeSymbols))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a given type is a primitive type, matching the runtime definition of one.
    /// </summary>
    /// <param name="typeSymbol">The type to check.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> is a primitive type.</returns>
    private static bool IsPrimitive(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType is
            SpecialType.System_Boolean or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_IntPtr or
            SpecialType.System_UIntPtr or
            SpecialType.System_Single or
            SpecialType.System_Double;
    }
}
