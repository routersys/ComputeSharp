using System.Collections.Immutable;
using System.Linq;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a graphics resource parameter of a compute pipeline method is missing [ComputeResource].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingComputeResourceAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MissingComputeResourceAttribute];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipeline], [ComputeResource] and IGraphicsResource symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.IGraphicsResource") is not { } graphicsResourceSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                // If the current method does not have the [ComputePipeline] attribute, there is nothing to do
                if (!methodSymbol.TryGetAttributeWithType(pipelineAttributeSymbol, out _))
                {
                    return;
                }

                // Each graphics resource parameter must be annotated with [ComputeResource]
                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                {
                    if (ImplementsGraphicsResource(parameterSymbol.Type, graphicsResourceSymbol) &&
                        !parameterSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out _))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MissingComputeResourceAttribute,
                            parameterSymbol.Locations[0],
                            parameterSymbol));
                    }
                }
            }, SymbolKind.Method);
        });
    }

    /// <summary>
    /// Checks whether a given type is a graphics resource type.
    /// </summary>
    /// <param name="typeSymbol">The input type to check.</param>
    /// <param name="graphicsResourceSymbol">The <see cref="IGraphicsResource"/> symbol.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> is a graphics resource type.</returns>
    private static bool ImplementsGraphicsResource(ITypeSymbol typeSymbol, INamedTypeSymbol graphicsResourceSymbol)
    {
        return typeSymbol.AllInterfaces.Any(interfaceSymbol => SymbolEqualityComparer.Default.Equals(interfaceSymbol, graphicsResourceSymbol));
    }
}
