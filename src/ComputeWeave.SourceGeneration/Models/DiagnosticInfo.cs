using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGeneration.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ComputeWeave.SourceGeneration.Models;

/// <summary>
/// A model for a serializeable diagnostic info.
/// </summary>
/// <param name="Descriptor">The wrapped <see cref="DiagnosticDescriptor"/> instance.</param>
/// <param name="SyntaxTree">The tree to use as location for the diagnostic, if available.</param>
/// <param name="TextSpan">The span to use as location for the diagnostic.</param>
/// <param name="ExternalLocation">The location to use for the diagnostic when it points at a file rather than a tree.</param>
/// <param name="Arguments">The diagnostic arguments.</param>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    SyntaxTree? SyntaxTree,
    TextSpan TextSpan,
    LocationInfo? ExternalLocation,
    EquatableArray<string> Arguments)
{
    /// <summary>
    /// Creates a new <see cref="Diagnostic"/> instance with the state from this model.
    /// </summary>
    /// <param name="compilation">The <see cref="Compilation"/> the diagnostic is reported for.</param>
    /// <returns>A new <see cref="Diagnostic"/> instance with the state from this model.</returns>
    public Diagnostic ToDiagnostic(Compilation compilation)
    {
        if (SyntaxTree is not null)
        {
            return Diagnostic.Create(Descriptor, Location.Create(SyntaxTree, TextSpan), Arguments.ToArray());
        }

        return Diagnostic.Create(Descriptor, ExternalLocation?.ToLocation(compilation), Arguments.ToArray());
    }

    /// <summary>
    /// Creates a new <see cref="DiagnosticInfo"/> instance with the specified parameters.
    /// </summary>
    /// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
    /// <param name="location">The location to use for the diagnostic.</param>
    /// <param name="args">The optional arguments for the formatted message to include.</param>
    /// <returns>A new <see cref="DiagnosticInfo"/> instance with the specified parameters.</returns>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object[] args)
    {
        // The returned DiagnosticInfo instances will be used inside incremental collections. Because of
        // that, we pre-transform all arguments with ToString(), so they are guaranteed to be equatable
        // from the start and not to keep compilations alive (if eg. they happen to be symbol objects).
        EquatableArray<string> textArgs = args.Select(static arg => arg.ToString()).ToImmutableArray();

        if (location is null)
        {
            return new(descriptor, null, default, null, textArgs);
        }

        // A location that points at a file instead of a tree is captured by value, a metadata one pointing at none
        if (location.SourceTree is null)
        {
            return new(descriptor, null, default, GetExternalLocation(location), textArgs);
        }

        return new(descriptor, location.SourceTree, location.SourceSpan, null, textArgs);
    }

    /// <summary>
    /// Creates a new <see cref="DiagnosticInfo"/> instance with the specified parameters.
    /// </summary>
    /// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
    /// <param name="symbol">The source <see cref="ISymbol"/> to attach the diagnostics to.</param>
    /// <param name="args">The optional arguments for the formatted message to include.</param>
    /// <returns>A new <see cref="DiagnosticInfo"/> instance with the specified parameters.</returns>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
    {
        return Create(descriptor, symbol.Locations.FirstOrDefault(), args);
    }

    /// <summary>
    /// Creates a new <see cref="DiagnosticInfo"/> instance with the specified parameters.
    /// </summary>
    /// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
    /// <param name="node">The source <see cref="SyntaxNode"/> to attach the diagnostics to.</param>
    /// <param name="args">The optional arguments for the formatted message to include.</param>
    /// <returns>A new <see cref="DiagnosticInfo"/> instance with the specified parameters.</returns>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] args)
    {
        return Create(descriptor, node.GetLocation(), args);
    }

    /// <summary>
    /// Gets the <see cref="LocationInfo"/> for a <see cref="Location"/> that belongs to no <see cref="SyntaxTree"/>.
    /// </summary>
    /// <param name="location">The <see cref="Location"/> value to capture.</param>
    /// <returns>A <see cref="LocationInfo"/> instance for <paramref name="location"/>, if it names a file.</returns>
    private static LocationInfo? GetExternalLocation(Location location)
    {
        if (location.Kind is not LocationKind.ExternalFile)
        {
            return null;
        }

        FileLinePositionSpan lineSpan = location.GetLineSpan();

        return new LocationInfo(lineSpan.Path, location.SourceSpan, lineSpan.Span);
    }
}