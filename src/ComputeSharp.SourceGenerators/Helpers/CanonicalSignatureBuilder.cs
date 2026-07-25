using System;
using System.Globalization;
using System.Text;
using ComputeSharp.SourceGeneration.Helpers;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// A builder for canonical method signatures, as defined by the descriptor serialization rules.
/// </summary>
internal static class CanonicalSignatureBuilder
{
    /// <summary>
    /// The separator between top level signature components.
    /// </summary>
    private const char ComponentSeparator = '|';

    /// <summary>
    /// The separator between a parameter ref kind and its type.
    /// </summary>
    private const char ParameterSeparator = ':';

    /// <summary>
    /// Tries to get the canonical signature for a given <see cref="IMethodSymbol"/> instance.
    /// </summary>
    /// <param name="symbol">The input <see cref="IMethodSymbol"/> instance.</param>
    /// <param name="signature">The resulting canonical signature, if it could be built.</param>
    /// <returns>Whether the canonical signature for <paramref name="symbol"/> could be built.</returns>
    public static bool TryGetCanonicalSignature(IMethodSymbol symbol, out string signature)
    {
        using ImmutableArrayBuilder<char> builder = new();

        if (!TryAppendComponent(CanonicalTypeNameBuilder.GetCanonicalTypeName(symbol.ContainingType), in builder))
        {
            signature = "";

            return false;
        }

        builder.Add(ComponentSeparator);

        if (!TryAppendComponent(symbol.MetadataName, in builder))
        {
            signature = "";

            return false;
        }

        builder.Add(ComponentSeparator);
        builder.AddRange(((uint)symbol.Arity).ToString("X8", CultureInfo.InvariantCulture).AsSpan());
        builder.Add(ComponentSeparator);

        if (!TryAppendComponent(CanonicalTypeNameBuilder.GetCanonicalTypeName(symbol.ReturnType), in builder))
        {
            signature = "";

            return false;
        }

        builder.Add(ComponentSeparator);
        builder.AddRange(((uint)symbol.Parameters.Length).ToString("X8", CultureInfo.InvariantCulture).AsSpan());

        foreach (IParameterSymbol parameterSymbol in symbol.Parameters)
        {
            if (!TryGetRefKindValue(parameterSymbol.RefKind, out byte refKindValue))
            {
                signature = "";

                return false;
            }

            builder.Add(ComponentSeparator);
            builder.AddRange(refKindValue.ToString("X2", CultureInfo.InvariantCulture).AsSpan());
            builder.Add(ParameterSeparator);

            if (!TryAppendComponent(CanonicalTypeNameBuilder.GetCanonicalTypeName(parameterSymbol.Type), in builder))
            {
                signature = "";

                return false;
            }
        }

        signature = builder.ToString();

        return true;
    }

    /// <summary>
    /// Tries to append a canonical component to a target builder, after normalizing it to form C.
    /// </summary>
    /// <param name="component">The canonical component to append.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    /// <returns>Whether <paramref name="component"/> could be appended.</returns>
    private static bool TryAppendComponent(string component, ref readonly ImmutableArrayBuilder<char> builder)
    {
        string normalizedComponent = component.IsNormalized(NormalizationForm.FormC)
            ? component
            : component.Normalize(NormalizationForm.FormC);

        if (CanonicalTypeNameBuilder.ContainsReservedCharacter(normalizedComponent))
        {
            return false;
        }

        builder.AddRange(normalizedComponent.AsSpan());

        return true;
    }

    /// <summary>
    /// Tries to get the canonical value for a given <see cref="RefKind"/> value.
    /// </summary>
    /// <param name="refKind">The input <see cref="RefKind"/> value.</param>
    /// <param name="value">The resulting canonical value, if it is supported.</param>
    /// <returns>Whether <paramref name="refKind"/> is supported.</returns>
    private static bool TryGetRefKindValue(RefKind refKind, out byte value)
    {
        switch (refKind)
        {
            case RefKind.None:
                value = 0;
                return true;
            case RefKind.Ref:
                value = 1;
                return true;
            case RefKind.Out:
                value = 2;
                return true;
            case RefKind.In:
                value = 3;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
