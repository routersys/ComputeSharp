using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a member declares more than one contract attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateContractAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DuplicateContractAttribute];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Only these two contract attributes share a target, as [ComputeResource] only targets parameters
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeSharedTextureAttribute") is not { } sharedTextureAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IFieldSymbol fieldSymbol ||
                    !fieldSymbol.HasAttributeWithType(resourceAttributeSymbol) ||
                    !fieldSymbol.HasAttributeWithType(sharedTextureAttributeSymbol))
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateContractAttribute,
                    fieldSymbol.Locations[0],
                    fieldSymbol));
            }, SymbolKind.Field);
        });
    }
}
