using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever the [GroupShared] attribute is used on an invalid target field.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidGroupSharedFieldDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidGroupSharedFieldDeclaration];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the IComputeShader, IComputeShader<TPixel> and [GloballyCoherent] symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.IComputeShader") is not { } computeShaderSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.IComputeShader`1") is not { } pixelShaderSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.GroupSharedAttribute") is not { } groupSharedAttribute)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IFieldSymbol { ContainingType: { } typeSymbol } fieldSymbol)
                {
                    return;
                }

                // If the current type does not have the [GroupShared] attribute, there is nothing to do
                if (!fieldSymbol.TryGetAttributeWithType(groupSharedAttribute, out AttributeData? attribute))
                {
                    return;
                }

                // Emit a diagnostic if the field is not valid (it must be static, of an array type with an unmanaged element type, and within a compute shader type)
                if (fieldSymbol is not { IsStatic: true, Type: IArrayTypeSymbol { ElementType: INamedTypeSymbol { IsUnmanagedType: true } } } ||
                    !MissingComputeShaderDescriptorOnComputeShaderAnalyzer.IsComputeShaderType(typeSymbol, computeShaderSymbol, pixelShaderSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidGroupSharedFieldDeclaration,
                        attribute.GetLocation(),
                        fieldSymbol));
                }
            }, SymbolKind.Field);
        });
    }
}