using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever an owned resource is not declared through a slot with a recovery contract.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidOwnedResourceDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InvalidOwnedResourceSlotDeclaration, MissingOwnedResourceRecoveryContract];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the attribute and slot symbols the owned resources of a host are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceSlot`1") is not { } resourceSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupSlot`1") is not { } resourceGroupSlotSymbol)
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
                    if (memberSymbol is not IFieldSymbol fieldSymbol ||
                        !fieldSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out AttributeData? attribute) ||
                        !PipelineResourceContractReader.TryRead(attribute, out _, out bool hasRecovery, out _))
                    {
                        continue;
                    }

                    bool isSlot =
                        fieldSymbol.Type is INamedTypeSymbol { IsGenericType: true } slotTypeSymbol &&
                        (SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceSlotSymbol) ||
                         SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceGroupSlotSymbol));

                    // A recovery contract declares the resource as owned, so it has to be held by a slot
                    if (hasRecovery && !isSlot)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidOwnedResourceSlotDeclaration,
                            fieldSymbol.Locations[0],
                            fieldSymbol,
                            fieldSymbol.Type));
                    }

                    // A slot always owns the resource it holds, so it has to declare how the contents are recovered
                    if (isSlot && !hasRecovery)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MissingOwnedResourceRecoveryContract,
                            fieldSymbol.Locations[0],
                            fieldSymbol));
                    }
                }
            }, SymbolKind.NamedType);
        });
    }
}
