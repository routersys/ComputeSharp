using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a [ComputeSharedTexture] field declares an invalid compute access.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidComputeSharedTextureFieldDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidComputeSharedTextureComputeAccess];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputeSharedTexture] symbol
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeSharedTextureAttribute") is not { } sharedTextureAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IFieldSymbol fieldSymbol)
                {
                    return;
                }

                // If the current field does not have the [ComputeSharedTexture] attribute, there is nothing to do
                if (!fieldSymbol.TryGetAttributeWithType(sharedTextureAttributeSymbol, out AttributeData? attribute))
                {
                    return;
                }

                // The compute access (the second constructor argument) must be ReadWrite
                if (attribute.ConstructorArguments is [_, { Value: byte computeAccess }, ..] &&
                    (ComputeResourceAccess)computeAccess is not ComputeResourceAccess.ReadWrite)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputeSharedTextureComputeAccess,
                        attribute.GetLocation(),
                        fieldSymbol));
                }
            }, SymbolKind.Field);
        });
    }
}
