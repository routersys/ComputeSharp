using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a compute pipeline host owns a disposable field other than a slot.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedOwnedDisposableFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [UnsupportedOwnedDisposableFieldInPipelineHost];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the symbols the declared members of a host are recognized with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.GraphicsDevice") is not { } graphicsDeviceSymbol ||
                context.Compilation.GetTypeByMetadataName("System.IDisposable") is not { } disposableSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol ||
                    !typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out _))
                {
                    return;
                }

                foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
                {
                    // The device field and every contract member are declared by the host grammar, and neither
                    // of them is owned by the host. Only the remaining instance state is owned by the user
                    if (memberSymbol is not IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } fieldSymbol ||
                        SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, graphicsDeviceSymbol) ||
                        fieldSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out _))
                    {
                        continue;
                    }

                    if (!fieldSymbol.Type.HasInterfaceWithType(disposableSymbol))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedOwnedDisposableFieldInPipelineHost,
                        fieldSymbol.Locations[0],
                        typeSymbol,
                        fieldSymbol));
                }
            }, SymbolKind.NamedType);
        });
    }
}
