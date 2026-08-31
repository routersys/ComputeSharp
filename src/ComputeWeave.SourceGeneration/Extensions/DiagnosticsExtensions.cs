using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGeneration.Models;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGeneration.Extensions;

/// <summary>
/// Extension methods for <see cref="GeneratorExecutionContext"/>, specifically for reporting diagnostics.
/// </summary>
internal static class DiagnosticsExtensions
{
    /// <summary>
    /// Adds a new diagnostics to the target builder.
    /// </summary>
    /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
    /// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
    /// <param name="symbol">The source <see cref="ISymbol"/> to attach the diagnostics to.</param>
    /// <param name="args">The optional arguments for the formatted message to include.</param>
    public static void Add(
        this ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        params object[] args)
    {
        diagnostics.Add(DiagnosticInfo.Create(descriptor, symbol, args));
    }

    /// <summary>
    /// Adds a new diagnostics to the target builder.
    /// </summary>
    /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
    /// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
    /// <param name="node">The source <see cref="SyntaxNode"/> to attach the diagnostics to.</param>
    /// <param name="args">The optional arguments for the formatted message to include.</param>
    public static void Add(
        this ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
        DiagnosticDescriptor descriptor,
        SyntaxNode node,
        params object[] args)
    {
        diagnostics.Add(DiagnosticInfo.Create(descriptor, node, args));
    }

    /// <summary>
    /// Checks whether the target builder holds a diagnostic that refuses the input it was produced for.
    /// </summary>
    /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
    /// <returns>Whether any diagnostic in <paramref name="diagnostics"/> refuses the input.</returns>
    /// <remarks>
    /// What is read is the default severity and not the effective one, because it records whether the construct
    /// can be translated at all, which a consumer lowering the severity in configuration does not change.
    /// </remarks>
    public static bool HasAnyErrors(this ImmutableArrayBuilder<DiagnosticInfo> diagnostics)
    {
        foreach (DiagnosticInfo diagnostic in diagnostics.WrittenSpan)
        {
            if (diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Registers an output node into an <see cref="IncrementalGeneratorInitializationContext"/> to output diagnostics.
    /// </summary>
    /// <param name="context">The input <see cref="IncrementalGeneratorInitializationContext"/> instance.</param>
    /// <param name="diagnostics">The input <see cref="IncrementalValuesProvider{TValues}"/> sequence of diagnostics.</param>
    public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<EquatableArray<DiagnosticInfo>> diagnostics)
    {
        context.RegisterSourceOutput(diagnostics, static (context, diagnostics) =>
        {
            foreach (DiagnosticInfo diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        });
    }
}