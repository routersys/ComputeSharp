using System;
using ComputeSharp.SourceGeneration.Helpers;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// A builder for canonical type metadata names, as defined by the descriptor serialization rules.
/// </summary>
internal static class CanonicalTypeNameBuilder
{
    /// <summary>
    /// The canonical metadata name used for the <see langword="dynamic"/> type.
    /// </summary>
    private const string DynamicTypeMetadataName = "System.Object";

    /// <summary>
    /// Gets the canonical metadata name for a given <see cref="ITypeSymbol"/> instance.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance.</param>
    /// <returns>The canonical metadata name for <paramref name="symbol"/>.</returns>
    public static string GetCanonicalTypeName(ITypeSymbol symbol)
    {
        using ImmutableArrayBuilder<char> builder = new();

        AppendCanonicalTypeName(symbol, in builder);

        return builder.ToString();
    }

    /// <summary>
    /// Appends the canonical metadata name for a given <see cref="ITypeSymbol"/> instance to a target builder.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    public static void AppendCanonicalTypeName(ITypeSymbol symbol, ref readonly ImmutableArrayBuilder<char> builder)
    {
        switch (symbol)
        {
            case IDynamicTypeSymbol:
                builder.AddRange(DynamicTypeMetadataName.AsSpan());
                break;
            case IArrayTypeSymbol arraySymbol:
                AppendCanonicalTypeName(arraySymbol.ElementType, in builder);
                builder.Add('[');

                for (int i = 1; i < arraySymbol.Rank; i++)
                {
                    builder.Add(',');
                }

                builder.Add(']');
                break;
            case INamedTypeSymbol { IsTupleType: true, TupleUnderlyingType: INamedTypeSymbol underlyingSymbol }:
                AppendCanonicalTypeName(underlyingSymbol, in builder);
                break;
            case INamedTypeSymbol namedSymbol:
                AppendCanonicalNamedTypeName(namedSymbol, in builder);
                break;
            default:
                builder.AddRange(symbol.MetadataName.AsSpan());
                break;
        }
    }

    /// <summary>
    /// Appends the canonical metadata name for a given <see cref="INamedTypeSymbol"/> instance to a target builder.
    /// </summary>
    /// <param name="symbol">The input <see cref="INamedTypeSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    private static void AppendCanonicalNamedTypeName(INamedTypeSymbol symbol, ref readonly ImmutableArrayBuilder<char> builder)
    {
        AppendDefinitionName(symbol, in builder);

        using ImmutableArrayBuilder<ITypeSymbol> typeArguments = new();

        AppendTypeArguments(symbol, in typeArguments);

        if (typeArguments.Count == 0)
        {
            return;
        }

        builder.Add('[');

        for (int i = 0; i < typeArguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Add(',');
            }

            AppendCanonicalTypeName(typeArguments.WrittenSpan[i], in builder);
        }

        builder.Add(']');
    }

    /// <summary>
    /// Appends the definition metadata name for a given <see cref="INamedTypeSymbol"/> instance to a target builder.
    /// </summary>
    /// <param name="symbol">The input <see cref="INamedTypeSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    private static void AppendDefinitionName(INamedTypeSymbol symbol, ref readonly ImmutableArrayBuilder<char> builder)
    {
        if (symbol.ContainingType is INamedTypeSymbol containingTypeSymbol)
        {
            AppendDefinitionName(containingTypeSymbol, in builder);
            builder.Add('+');
        }
        else if (symbol.ContainingNamespace is INamespaceSymbol { IsGlobalNamespace: false } namespaceSymbol)
        {
            AppendNamespaceName(namespaceSymbol, in builder);
            builder.Add('.');
        }

        builder.AddRange(symbol.MetadataName.AsSpan());
    }

    /// <summary>
    /// Appends the metadata name for a given <see cref="INamespaceSymbol"/> instance to a target builder.
    /// </summary>
    /// <param name="symbol">The input <see cref="INamespaceSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    private static void AppendNamespaceName(INamespaceSymbol symbol, ref readonly ImmutableArrayBuilder<char> builder)
    {
        if (symbol.ContainingNamespace is INamespaceSymbol { IsGlobalNamespace: false } containingNamespaceSymbol)
        {
            AppendNamespaceName(containingNamespaceSymbol, in builder);
            builder.Add('.');
        }

        builder.AddRange(symbol.MetadataName.AsSpan());
    }

    /// <summary>
    /// Appends the type arguments for a given <see cref="INamedTypeSymbol"/> instance, from the outermost containing type inwards.
    /// </summary>
    /// <param name="symbol">The input <see cref="INamedTypeSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    private static void AppendTypeArguments(INamedTypeSymbol symbol, ref readonly ImmutableArrayBuilder<ITypeSymbol> builder)
    {
        if (symbol.ContainingType is INamedTypeSymbol containingTypeSymbol)
        {
            AppendTypeArguments(containingTypeSymbol, in builder);
        }

        foreach (ITypeSymbol typeArgumentSymbol in symbol.TypeArguments)
        {
            if (typeArgumentSymbol is ITypeParameterSymbol)
            {
                continue;
            }

            builder.Add(typeArgumentSymbol);
        }
    }

    /// <summary>
    /// Checks whether a given canonical component contains a reserved separator character.
    /// </summary>
    /// <param name="component">The canonical component to check.</param>
    /// <returns>Whether <paramref name="component"/> contains a reserved separator character.</returns>
    public static bool ContainsReservedCharacter(string component)
    {
        return component.IndexOf('|') >= 0 || component.IndexOf(':') >= 0;
    }
}
