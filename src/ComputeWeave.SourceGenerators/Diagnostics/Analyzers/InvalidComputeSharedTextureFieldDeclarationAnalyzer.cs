using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a [ComputeSharedTexture] field declares an invalid compute access.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidComputeSharedTextureFieldDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InvalidComputeSharedTextureComputeAccess, InvalidComputeSharedTextureFieldDeclaration];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputeSharedTexture] symbol
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeSharedTextureAttribute") is not { } sharedTextureAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.SharedTextureSlot`3") is not { } sharedTextureSlotSymbol)
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

                // The field must be a private readonly instance field of a shared texture slot type, without an initializer
                if (fieldSymbol is not { DeclaredAccessibility: Accessibility.Private, IsReadOnly: true, IsStatic: false } ||
                    fieldSymbol.Type is not INamedTypeSymbol { IsGenericType: true } fieldTypeSymbol ||
                    !SymbolEqualityComparer.Default.Equals(fieldTypeSymbol.ConstructedFrom, sharedTextureSlotSymbol) ||
                    HasInitializer(fieldSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputeSharedTextureFieldDeclaration,
                        fieldSymbol.Locations.FirstOrDefault(),
                        fieldSymbol));

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

    /// <summary>
    /// Checks whether a given field has an initializer.
    /// </summary>
    /// <param name="fieldSymbol">The input field to check.</param>
    /// <returns>Whether <paramref name="fieldSymbol"/> has an initializer.</returns>
    private static bool HasInitializer(IFieldSymbol fieldSymbol)
    {
        foreach (SyntaxReference syntaxReference in fieldSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null })
            {
                return true;
            }
        }

        return false;
    }
}
