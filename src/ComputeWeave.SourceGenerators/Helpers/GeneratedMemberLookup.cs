using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGenerators.Helpers;

/// <summary>
/// The lookup of the members a type declares against the ones the generators produce for it.
/// </summary>
internal static class GeneratedMemberLookup
{
    /// <summary>
    /// Checks whether a type declares a member with a given name that is not generated.
    /// </summary>
    /// <param name="typeSymbol">The type declaring the member.</param>
    /// <param name="name">The name of the member to look for.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> declares a member named <paramref name="name"/> that is not generated.</returns>
    public static bool IsDeclaredByUser(INamedTypeSymbol typeSymbol, string name, INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(name))
        {
            if (!memberSymbol.HasAttributeWithType(generatedCodeAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
