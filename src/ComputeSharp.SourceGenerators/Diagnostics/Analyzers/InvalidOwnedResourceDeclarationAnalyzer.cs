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
        [InvalidOwnedResourceSlotDeclaration, MissingOwnedResourceRecoveryContract, DuplicateResourceGroupMemberRecoveryContract, UnsupportedResourcePlanMember];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the attribute and slot symbols the owned resources of a host are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupAttribute") is not { } resourceGroupAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceSlot`1") is not { } resourceSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupSlot`1") is not { } resourceGroupSlotSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                // A recovery contract belongs to the slot holding the group, so the members must not declare one
                if (typeSymbol.TryGetAttributeWithType(resourceGroupAttributeSymbol, out _))
                {
                    foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
                    {
                        if (memberSymbol is not IPropertySymbol propertySymbol ||
                            !memberSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out AttributeData? memberAttribute) ||
                            !PipelineResourceContractReader.TryRead(memberAttribute, out _, out bool memberHasRecovery, out _))
                        {
                            continue;
                        }

                        if (memberHasRecovery)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DuplicateResourceGroupMemberRecoveryContract,
                                memberSymbol.Locations[0],
                                memberSymbol));
                        }

                        ReportIfPlanIsUnsupported(context, memberSymbol, propertySymbol.Type);
                    }

                    return;
                }

                if (!typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out _))
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

                    bool isResourceSlot =
                        fieldSymbol.Type is INamedTypeSymbol { IsGenericType: true } resourceSlotTypeSymbol &&
                        SymbolEqualityComparer.Default.Equals(resourceSlotTypeSymbol.OriginalDefinition, resourceSlotSymbol);

                    bool isSlot =
                        isResourceSlot ||
                        (fieldSymbol.Type is INamedTypeSymbol { IsGenericType: true } groupSlotTypeSymbol &&
                         SymbolEqualityComparer.Default.Equals(groupSlotTypeSymbol.OriginalDefinition, resourceGroupSlotSymbol));

                    // The resource of a slot is created from its exact plan, while the plan of a group
                    // slot is the concatenation of the dimensions its members declare on the group type
                    if (isResourceSlot)
                    {
                        ReportIfPlanIsUnsupported(context, fieldSymbol, ((INamedTypeSymbol)fieldSymbol.Type).TypeArguments[0]);
                    }

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

    /// <summary>
    /// Reports a diagnostic if the declared type of an owned resource has no resource plan.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="memberSymbol">The member declaring the owned resource.</param>
    /// <param name="resourceTypeSymbol">The declared type of the owned resource.</param>
    private static void ReportIfPlanIsUnsupported(SymbolAnalysisContext context, ISymbol memberSymbol, ITypeSymbol resourceTypeSymbol)
    {
        if (ResourcePlanGrammar.TryGetPlanKind(resourceTypeSymbol, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            UnsupportedResourcePlanMember,
            memberSymbol.Locations[0],
            memberSymbol,
            resourceTypeSymbol));
    }
}
