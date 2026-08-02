using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever an owned resource parameter has an invalid declaration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidOwnedResourceParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InvalidOwnedResourceParameterDeclaration, InvalidOwnedResourceParameterType];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the attribute and slot symbols an owned resource parameter is declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeOwnedResourceAttribute") is not { } ownedResourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineResourceAttribute") is not { } pipelineResourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceSlot`1") is not { } resourceSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceGroupSlot`1") is not { } resourceGroupSlotSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                bool isPipeline = methodSymbol.HasAttributeWithType(pipelineAttributeSymbol);

                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                {
                    if (!parameterSymbol.TryGetAttributeWithType(ownedResourceAttributeSymbol, out AttributeData? attribute))
                    {
                        continue;
                    }

                    if (!isPipeline ||
                        parameterSymbol.HasAttributeWithType(resourceAttributeSymbol) ||
                        attribute.ConstructorArguments is not [{ Value: string slotFieldName }] ||
                        !TryGetSlotType(methodSymbol.ContainingType, slotFieldName, pipelineResourceAttributeSymbol, resourceSlotSymbol, resourceGroupSlotSymbol, out ITypeSymbol? slotTypeSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidOwnedResourceParameterDeclaration,
                            parameterSymbol.Locations[0],
                            parameterSymbol));

                        continue;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(parameterSymbol.Type, slotTypeSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidOwnedResourceParameterType,
                            parameterSymbol.Locations[0],
                            parameterSymbol,
                            parameterSymbol.Type,
                            slotFieldName,
                            slotTypeSymbol));
                    }
                }
            }, SymbolKind.Method);
        });
    }

    /// <summary>
    /// Tries to get the type an owned resource slot declared by a given host field provides.
    /// </summary>
    /// <param name="hostSymbol">The type declaring the pipeline method.</param>
    /// <param name="slotFieldName">The name of the field declaring the owned resource slot.</param>
    /// <param name="pipelineResourceAttributeSymbol">The <c>[ComputePipelineResource]</c> symbol.</param>
    /// <param name="resourceSlotSymbol">The <c>ComputeResourceSlot&lt;TResource&gt;</c> symbol.</param>
    /// <param name="resourceGroupSlotSymbol">The <c>ComputeResourceGroupSlot&lt;TGroup&gt;</c> symbol.</param>
    /// <param name="slotTypeSymbol">The resulting provided type, if <paramref name="slotFieldName"/> declares an owned resource slot.</param>
    /// <returns>Whether <paramref name="slotFieldName"/> declares an owned resource slot.</returns>
    private static bool TryGetSlotType(
        INamedTypeSymbol hostSymbol,
        string slotFieldName,
        INamedTypeSymbol pipelineResourceAttributeSymbol,
        INamedTypeSymbol resourceSlotSymbol,
        INamedTypeSymbol resourceGroupSlotSymbol,
        out ITypeSymbol? slotTypeSymbol)
    {
        foreach (ISymbol memberSymbol in hostSymbol.GetMembers(slotFieldName))
        {
            if (memberSymbol is IFieldSymbol { Type: INamedTypeSymbol { IsGenericType: true } fieldTypeSymbol } fieldSymbol &&
                fieldSymbol.HasAttributeWithType(pipelineResourceAttributeSymbol) &&
                (SymbolEqualityComparer.Default.Equals(fieldTypeSymbol.OriginalDefinition, resourceSlotSymbol) ||
                 SymbolEqualityComparer.Default.Equals(fieldTypeSymbol.OriginalDefinition, resourceGroupSlotSymbol)))
            {
                slotTypeSymbol = fieldTypeSymbol.TypeArguments[0];

                return true;
            }
        }

        slotTypeSymbol = null;

        return false;
    }
}
