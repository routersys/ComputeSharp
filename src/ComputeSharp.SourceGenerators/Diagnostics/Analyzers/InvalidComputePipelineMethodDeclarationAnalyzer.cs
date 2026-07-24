using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates errors for invalid uses of [ComputePipeline] on a method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidComputePipelineMethodDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        InvalidComputePipelineMethodSignature,
        UnsupportedComputePipelineMethodForm
    ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipeline] and ComputeContext symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeContext") is not { } computeContextSymbol)
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

                // The method cannot be static, generic or async (a void method cannot be an iterator)
                if (methodSymbol.IsStatic ||
                    methodSymbol.IsGenericMethod ||
                    methodSymbol.IsAsync)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedComputePipelineMethodForm,
                        methodSymbol.Locations[0],
                        methodSymbol));
                }

                // The method must return void, take an 'in ComputeContext' as its first parameter,
                // and only declare value or 'in' parameters otherwise
                if (!methodSymbol.ReturnsVoid ||
                    methodSymbol.Parameters is not [{ RefKind: RefKind.In, Type: INamedTypeSymbol firstParameterType }, ..] ||
                    !SymbolEqualityComparer.Default.Equals(firstParameterType, computeContextSymbol) ||
                    !AreTrailingParametersValid(methodSymbol.Parameters))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputePipelineMethodSignature,
                        methodSymbol.Locations[0],
                        methodSymbol));
                }
            }, SymbolKind.Method);
        });
    }

    /// <summary>
    /// Checks whether all parameters after the first one are passed by value or by <see langword="in"/> reference.
    /// </summary>
    /// <param name="parameters">The parameters of the compute pipeline method.</param>
    /// <returns>Whether all parameters after the first one are passed by value or by <see langword="in"/> reference.</returns>
    private static bool AreTrailingParametersValid(ImmutableArray<IParameterSymbol> parameters)
    {
        for (int i = 1; i < parameters.Length; i++)
        {
            if (parameters[i].RefKind is not (RefKind.None or RefKind.In))
            {
                return false;
            }
        }

        return true;
    }
}
