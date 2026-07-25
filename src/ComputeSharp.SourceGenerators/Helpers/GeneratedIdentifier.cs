using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The generation rules for the canonical identifiers derived from owned member names.
/// </summary>
internal static class GeneratedIdentifier
{
    /// <summary>
    /// Tries to create the generated canonical name for a given owned member name.
    /// </summary>
    /// <param name="sourceName">The source name of the owned member.</param>
    /// <param name="canonicalName">The resulting generated canonical name, if one could be created.</param>
    /// <returns>Whether a generated canonical name could be created for <paramref name="sourceName"/>.</returns>
    public static bool TryCreateCanonicalName(string sourceName, out string canonicalName)
    {
        ReadOnlySpan<char> remaining = sourceName.AsSpan();

        if (remaining.Length > 0 && remaining[0] is '@')
        {
            remaining = remaining[1..];
        }

        while (remaining.Length > 0 && remaining[0] is '_')
        {
            remaining = remaining[1..];
        }

        if (remaining.Length == 0)
        {
            canonicalName = "";

            return false;
        }

        using ImmutableArrayBuilder<char> builder = new();

        builder.Add(char.ToUpperInvariant(remaining[0]));
        builder.AddRange(remaining[1..]);

        canonicalName = builder.ToString();

        return true;
    }

    /// <summary>
    /// Creates the generated plan parameter name for a given canonical member name and dimension.
    /// </summary>
    /// <param name="canonicalMemberName">The generated canonical name of the owned member.</param>
    /// <param name="dimensionKind">The dimension the parameter carries.</param>
    /// <returns>The generated plan parameter name.</returns>
    public static string CreatePlanParameterName(string canonicalMemberName, ResourcePlanDimensionKind dimensionKind)
    {
        using ImmutableArrayBuilder<char> builder = new();

        builder.Add(char.ToLowerInvariant(canonicalMemberName[0]));
        builder.AddRange(canonicalMemberName.AsSpan(1));
        builder.AddRange(GetDimensionName(dimensionKind).AsSpan());

        return builder.ToString();
    }

    /// <summary>
    /// Creates the generated plan property name for a given canonical member name and dimension.
    /// </summary>
    /// <param name="canonicalMemberName">The generated canonical name of the owned member.</param>
    /// <param name="dimensionKind">The dimension the property carries.</param>
    /// <returns>The generated plan property name.</returns>
    public static string CreatePlanPropertyName(string canonicalMemberName, ResourcePlanDimensionKind dimensionKind)
    {
        return canonicalMemberName + GetDimensionName(dimensionKind);
    }

    /// <summary>
    /// Creates the generated plan type name for a given canonical member name.
    /// </summary>
    /// <param name="canonicalMemberName">The generated canonical name of the owned member.</param>
    /// <returns>The generated plan type name.</returns>
    public static string CreatePlanTypeName(string canonicalMemberName)
    {
        return canonicalMemberName + "Plan";
    }

    /// <summary>
    /// Gets the generated name for a given dimension.
    /// </summary>
    /// <param name="dimensionKind">The dimension to get the name for.</param>
    /// <returns>The generated name for <paramref name="dimensionKind"/>.</returns>
    public static string GetDimensionName(ResourcePlanDimensionKind dimensionKind)
    {
        return dimensionKind switch
        {
            ResourcePlanDimensionKind.Length => "Length",
            ResourcePlanDimensionKind.Width => "Width",
            ResourcePlanDimensionKind.Height => "Height",
            _ => throw new ArgumentOutOfRangeException(nameof(dimensionKind))
        };
    }
}
