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
    /// Drops the records of syntax with no verdict, when the input was refused for a reason of its own.
    /// </summary>
    /// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
    /// <param name="unknownSyntax">The descriptor for a syntax kind the accepted set records no verdict for.</param>
    /// <remarks>
    /// <para>
    /// A refused construct carries syntax the set has no verdict for, both under it and beside it: a refused
    /// <c>foreach</c> holds an array creation, and a refused local declaration sits next to one. Recording those
    /// as well names several places for one cause, and the place the author has to change is the one the refusal
    /// already names. The same reading is what stops a refused input from reaching the shader compiler.
    /// </para>
    /// <para>
    /// The reading is of the whole set produced for one shader, and not of the subtree a record sits in, so it
    /// does not depend on the order the rewriting walks in. Syntax with no verdict elsewhere in the same shader
    /// is dropped along with the rest, and is reported once the refusal is gone, the way a compiler stops
    /// reading a file after the error that makes the rest of it meaningless.
    /// </para>
    /// </remarks>
    public static void DropUnknownSyntaxAfterARefusal(
        this ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
        DiagnosticDescriptor unknownSyntax)
    {
        bool isRefused = false;

        foreach (DiagnosticInfo diagnostic in diagnostics.WrittenSpan)
        {
            if (diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error &&
                diagnostic.Descriptor != unknownSyntax)
            {
                isRefused = true;

                break;
            }
        }

        if (!isRefused)
        {
            return;
        }

        using ImmutableArrayBuilder<DiagnosticInfo> kept = new();

        foreach (DiagnosticInfo diagnostic in diagnostics.WrittenSpan)
        {
            if (diagnostic.Descriptor != unknownSyntax)
            {
                kept.Add(diagnostic);
            }
        }

        diagnostics.Clear();
        diagnostics.AddRange(kept.WrittenSpan);
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