using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a [ComputeInterop] method does not declare an external resource parameter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingExternalComputeResourceInInteropMethodAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MissingExternalComputeResourceInInteropMethod];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputeInterop] and [ComputeResource] symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeInteropAttribute") is not { } interopAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceAttribute") is not { } resourceAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                // If the current method does not have the [ComputeInterop] attribute, there is nothing to do
                if (!methodSymbol.TryGetAttributeWithType(interopAttributeSymbol, out _))
                {
                    return;
                }

                // At least one parameter must declare an external resource
                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                {
                    if (IsExternalResource(parameterSymbol, resourceAttributeSymbol))
                    {
                        return;
                    }
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    MissingExternalComputeResourceInInteropMethod,
                    methodSymbol.Locations[0],
                    methodSymbol));
            }, SymbolKind.Method);
        });
    }

    /// <summary>
    /// Checks whether a given parameter declares an external resource contract.
    /// </summary>
    /// <param name="parameterSymbol">The input parameter to check.</param>
    /// <param name="resourceAttributeSymbol">The <c>[ComputeResource]</c> symbol.</param>
    /// <returns>Whether <paramref name="parameterSymbol"/> declares an external resource contract.</returns>
    private static bool IsExternalResource(IParameterSymbol parameterSymbol, INamedTypeSymbol resourceAttributeSymbol)
    {
        if (!parameterSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out AttributeData? attribute))
        {
            return false;
        }

        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == "Sharing" && namedArgument.Value.Value is byte sharing)
            {
                return (ComputeResourceSharing)sharing is ComputeResourceSharing.External;
            }
        }

        return false;
    }
}
