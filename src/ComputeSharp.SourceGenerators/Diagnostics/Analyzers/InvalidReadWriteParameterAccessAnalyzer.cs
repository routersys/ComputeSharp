using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a pipeline parameter bound as read-write declares another access.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidReadWriteParameterAccessAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InvalidReadWriteParameterAccessContract];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipeline] and [ComputeResource] symbols the parameter contracts are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceAttribute") is not { } resourceAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IMethodSymbol methodSymbol ||
                    !methodSymbol.TryGetAttributeWithType(pipelineAttributeSymbol, out _))
                {
                    return;
                }

                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                {
                    if (!parameterSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out AttributeData? attribute) ||
                        !PipelineCollector.TryGetResourceContract(attribute, out ComputeResourceAccess access, out _, out _) ||
                        ComputeAccessContract.IsCompatible(parameterSymbol.Type, access))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidReadWriteParameterAccessContract,
                        parameterSymbol.Locations[0],
                        parameterSymbol,
                        parameterSymbol.Type));
                }
            }, SymbolKind.Method);
        });
    }
}
