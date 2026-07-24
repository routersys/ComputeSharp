using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a compute pipeline host declares an invalid device field.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidComputePipelineHostDeviceFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidComputePipelineHostDeviceField];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipelineHost] and GraphicsDevice symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.GraphicsDevice") is not { } graphicsDeviceSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                // If the current type does not have the [ComputePipelineHost] attribute, there is nothing to do
                if (!typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out AttributeData? attribute))
                {
                    return;
                }

                string? deviceFieldName = attribute.ConstructorArguments is [{ Value: string name }, ..] ? name : null;

                // The host must declare a 'private readonly GraphicsDevice' field with the configured name and no initializer
                if (deviceFieldName is null ||
                    !HasValidDeviceField(typeSymbol, deviceFieldName, graphicsDeviceSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputePipelineHostDeviceField,
                        attribute.GetLocation(),
                        typeSymbol,
                        deviceFieldName ?? ""));
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Checks whether a given type declares a valid device field with a given name.
    /// </summary>
    /// <param name="typeSymbol">The compute pipeline host type.</param>
    /// <param name="deviceFieldName">The configured device field name.</param>
    /// <param name="graphicsDeviceSymbol">The <see cref="GraphicsDevice"/> symbol.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> declares a valid device field named <paramref name="deviceFieldName"/>.</returns>
    private static bool HasValidDeviceField(INamedTypeSymbol typeSymbol, string deviceFieldName, INamedTypeSymbol graphicsDeviceSymbol)
    {
        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(deviceFieldName))
        {
            if (memberSymbol is IFieldSymbol fieldSymbol)
            {
                return fieldSymbol is { DeclaredAccessibility: Accessibility.Private, IsReadOnly: true, IsStatic: false } &&
                       SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, graphicsDeviceSymbol) &&
                       !HasInitializer(fieldSymbol);
            }
        }

        return false;
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
