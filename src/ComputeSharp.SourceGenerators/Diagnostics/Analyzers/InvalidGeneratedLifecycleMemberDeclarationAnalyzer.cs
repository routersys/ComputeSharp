using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a compute pipeline host or interop resource set declares a generated lifecycle member.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidGeneratedLifecycleMemberDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidGeneratedLifecycleMemberDeclaration];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipelineHost] and [ComputeInteropResourceSet] symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeInteropResourceSetAttribute") is not { } resourceSetAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                // Only compute pipeline hosts and interop resource sets are targets
                if (!typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out _) &&
                    !typeSymbol.TryGetAttributeWithType(resourceSetAttributeSymbol, out _))
                {
                    return;
                }

                foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
                {
                    if (memberSymbol is IMethodSymbol methodSymbol && IsGeneratedLifecycleMember(methodSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidGeneratedLifecycleMemberDeclaration,
                            methodSymbol.Locations[0],
                            typeSymbol,
                            methodSymbol));
                    }
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Checks whether a given method is a generated lifecycle member.
    /// </summary>
    /// <param name="methodSymbol">The input method to check.</param>
    /// <returns>Whether <paramref name="methodSymbol"/> is a generated lifecycle member.</returns>
    private static bool IsGeneratedLifecycleMember(IMethodSymbol methodSymbol)
    {
        // An explicitly declared instance constructor is generated
        if (methodSymbol is { MethodKind: MethodKind.Constructor, IsStatic: false, IsImplicitlyDeclared: false })
        {
            return true;
        }

        // A finalizer is generated
        if (methodSymbol.MethodKind == MethodKind.Destructor)
        {
            return true;
        }

        // A parameterless Dispose() or WaitForDisposal() method is generated
        return methodSymbol is { MethodKind: MethodKind.Ordinary, Parameters.IsEmpty: true, Name: "Dispose" or "WaitForDisposal" };
    }
}
