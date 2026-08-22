using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever an intrinsic that a compute shader cannot use is invoked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedHlslIntrinsicAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UnsupportedHlslIntrinsicInvocation];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.Hlsl") is not { } hlslSymbol)
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                if (context.Operation is not IInvocationOperation { TargetMethod: { IsStatic: true } methodSymbol })
                {
                    return;
                }

                if (!SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, hlslSymbol))
                {
                    return;
                }

                if (GetUnsupportedReason(methodSymbol.Name) is not { } reason)
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedHlslIntrinsicInvocation,
                    context.Operation.Syntax.GetLocation(),
                    methodSymbol.Name,
                    reason));
            }, OperationKind.Invocation);
        });
    }

    /// <summary>
    /// Gets the reason a given intrinsic cannot be used in a compute shader.
    /// </summary>
    /// <param name="name">The name of the intrinsic being invoked.</param>
    /// <returns>The reason the intrinsic cannot be used, or <see langword="null"/> if it can.</returns>
    private static string? GetUnsupportedReason(string name)
    {
        return name switch
        {
            "Abort" => "the abort intrinsic is not accepted by the DXC compiler",
            "Clip" => "the clip intrinsic discards a pixel, and a compute shader has no pixel to discard",
            "DerivativeOfDx" or
            "DerivativeOfDxHighPrecision" or
            "DerivativeOfDxLowPrecision" or
            "DerivativeOfDy" or
            "DerivativeOfDyHighPrecision" or
            "DerivativeOfDyLowPrecision" or
            "Fwidth" => "derivatives in a compute shader require shader model 6.6, while shaders are compiled as cs_6_0",
            _ => null
        };
    }
}
