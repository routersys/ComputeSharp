using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever an owned resource declares a type its access contract cannot produce.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidOwnedResourceTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InvalidOwnedSlotResourceType, InvalidResourceGroupMemberResourceType];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the attribute and slot symbols the owned resources are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceGroupAttribute") is not { } resourceGroupAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceSlot`1") is not { } resourceSlotSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                bool isHost = typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out _);

                // Only compute pipeline hosts and resource groups declare owned resources
                if (!isHost && !typeSymbol.TryGetAttributeWithType(resourceGroupAttributeSymbol, out _))
                {
                    return;
                }

                foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
                {
                    if (!memberSymbol.TryGetAttributeWithType(resourceAttributeSymbol, out AttributeData? attribute) ||
                        !PipelineResourceContractReader.TryRead(attribute, out ComputeResourceAccess access, out _, out _))
                    {
                        continue;
                    }

                    // Owned slots declare their resource type through the slot type argument, while resource group
                    // members declare it directly. Every other annotated member is out of scope for this analyzer
                    if (isHost)
                    {
                        if (memberSymbol is not IFieldSymbol { Type: INamedTypeSymbol slotTypeSymbol } ||
                            !SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceSlotSymbol))
                        {
                            continue;
                        }

                        ReportIfNotHeld(context, memberSymbol, slotTypeSymbol.TypeArguments[0], access, InvalidOwnedSlotResourceType);
                    }
                    else if (memberSymbol is IPropertySymbol propertySymbol)
                    {
                        ReportIfNotHeld(context, memberSymbol, propertySymbol.Type, access, InvalidResourceGroupMemberResourceType);
                    }
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Reports a diagnostic if a declared resource type cannot hold the resource its access contract produces.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="memberSymbol">The member declaring the owned resource.</param>
    /// <param name="declaredTypeSymbol">The declared type of the owned resource.</param>
    /// <param name="access">The declared compute access of the owned resource.</param>
    /// <param name="descriptor">The descriptor to report the diagnostic with.</param>
    private static void ReportIfNotHeld(
        SymbolAnalysisContext context,
        ISymbol memberSymbol,
        ITypeSymbol declaredTypeSymbol,
        ComputeResourceAccess access,
        DiagnosticDescriptor descriptor)
    {
        if (!MaterializedResourceType.TryGet(context.Compilation, declaredTypeSymbol, access, out INamedTypeSymbol materializedTypeSymbol) ||
            MaterializedResourceType.IsHeldBy(materializedTypeSymbol, declaredTypeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            memberSymbol.Locations[0],
            memberSymbol,
            declaredTypeSymbol,
            materializedTypeSymbol));
    }
}
