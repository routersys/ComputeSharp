using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGenerators.Helpers;

/// <summary>
/// The accessibility rules of the members generated for a declared type.
/// </summary>
internal static class GeneratedAccessibility
{
    /// <summary>
    /// Gets the accessibility keyword a generated member referencing a given type can be declared with.
    /// </summary>
    /// <param name="typeSymbol">The type referenced by the generated member.</param>
    /// <returns>The accessibility keyword for the generated member.</returns>
    public static string GetKeyword(ITypeSymbol typeSymbol)
    {
        return GetKeyword(GetEffectiveAccessibility(typeSymbol));
    }

    /// <summary>
    /// Gets the accessibility keyword for a given effective accessibility.
    /// </summary>
    /// <param name="accessibility">The effective accessibility of the generated member.</param>
    /// <returns>The accessibility keyword for the generated member.</returns>
    public static string GetKeyword(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private"
        };
    }

    /// <summary>
    /// Gets the effective accessibility of a given type, accounting for its containing types and type arguments.
    /// </summary>
    /// <param name="typeSymbol">The type to get the effective accessibility of.</param>
    /// <returns>The effective accessibility of <paramref name="typeSymbol"/>.</returns>
    public static Accessibility GetEffectiveAccessibility(ITypeSymbol typeSymbol)
    {
        Accessibility accessibility = Accessibility.Public;

        for (ITypeSymbol? currentSymbol = typeSymbol; currentSymbol is not null; currentSymbol = currentSymbol.ContainingType)
        {
            if (currentSymbol.DeclaredAccessibility is not Accessibility.NotApplicable &&
                currentSymbol.DeclaredAccessibility < accessibility)
            {
                accessibility = currentSymbol.DeclaredAccessibility;
            }

            if (currentSymbol is not INamedTypeSymbol namedTypeSymbol)
            {
                continue;
            }

            foreach (ITypeSymbol typeArgumentSymbol in namedTypeSymbol.TypeArguments)
            {
                Accessibility typeArgumentAccessibility = GetEffectiveAccessibility(typeArgumentSymbol);

                if (typeArgumentAccessibility < accessibility)
                {
                    accessibility = typeArgumentAccessibility;
                }
            }
        }

        return accessibility;
    }
}
