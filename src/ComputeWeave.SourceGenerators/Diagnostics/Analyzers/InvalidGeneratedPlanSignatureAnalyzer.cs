using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever an owned member cannot own its generated plan members.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidGeneratedPlanSignatureAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidGeneratedPlanSignature];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the attribute and slot symbols the owned members are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeInteropResourceSetAttribute") is not { } resourceSetAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceGroupAttribute") is not { } resourceGroupAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputePipelineResourceAttribute") is not { } resourceAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeSharedTextureAttribute") is not { } sharedTextureAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceSlot`1") is not { } resourceSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ComputeResourceGroupSlot`1") is not { } resourceGroupSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.SharedTextureSlot`3") is not { } sharedTextureSlotSymbol ||
                context.Compilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute") is not { } generatedCodeAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                if (typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out _))
                {
                    AnalyzeHost(context, typeSymbol, resourceAttributeSymbol, resourceSlotSymbol, resourceGroupSlotSymbol, generatedCodeAttributeSymbol);
                }
                else if (typeSymbol.TryGetAttributeWithType(resourceSetAttributeSymbol, out _))
                {
                    AnalyzeResourceSet(context, typeSymbol, sharedTextureAttributeSymbol, sharedTextureSlotSymbol, generatedCodeAttributeSymbol);
                }
                else if (typeSymbol.TryGetAttributeWithType(resourceGroupAttributeSymbol, out _))
                {
                    AnalyzeResourceGroup(context, typeSymbol, resourceAttributeSymbol, generatedCodeAttributeSymbol);
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Analyzes the owned slots of a compute pipeline host.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="typeSymbol">The compute pipeline host type.</param>
    /// <param name="resourceAttributeSymbol">The <c>[ComputePipelineResource]</c> symbol.</param>
    /// <param name="resourceSlotSymbol">The <c>ComputeResourceSlot&lt;TResource&gt;</c> symbol.</param>
    /// <param name="resourceGroupSlotSymbol">The <c>ComputeResourceGroupSlot&lt;TGroup&gt;</c> symbol.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    private static void AnalyzeHost(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol resourceAttributeSymbol,
        INamedTypeSymbol resourceSlotSymbol,
        INamedTypeSymbol resourceGroupSlotSymbol,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        using ImmutableArrayBuilder<IFieldSymbol> slotBuilder = new();

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
        {
            if (memberSymbol is IFieldSymbol { Type: INamedTypeSymbol slotTypeSymbol } fieldSymbol &&
                memberSymbol.HasAttributeWithType(resourceAttributeSymbol) &&
                (SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceSlotSymbol) ||
                 SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceGroupSlotSymbol)))
            {
                slotBuilder.Add(fieldSymbol);
            }
        }

        Dictionary<string, int> canonicalNameCounts = GetCanonicalNameCounts(slotBuilder.WrittenSpan);

        foreach (IFieldSymbol fieldSymbol in slotBuilder.WrittenSpan)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(fieldSymbol.MetadataName, out string canonicalName) ||
                canonicalNameCounts[canonicalName] > 1 ||
                IsGeneratedSlotMemberDeclared(
                    typeSymbol,
                    canonicalName,
                    IsResourceGroupSlot(fieldSymbol, resourceGroupSlotSymbol),
                    generatedCodeAttributeSymbol))
            {
                Report(context, fieldSymbol, typeSymbol);
            }
        }
    }

    /// <summary>
    /// Analyzes the owned slots of a compute interop resource set.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="typeSymbol">The compute interop resource set type.</param>
    /// <param name="sharedTextureAttributeSymbol">The <c>[ComputeSharedTexture]</c> symbol.</param>
    /// <param name="sharedTextureSlotSymbol">The <c>SharedTextureSlot&lt;T, TPixel, TView&gt;</c> symbol.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    private static void AnalyzeResourceSet(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol sharedTextureAttributeSymbol,
        INamedTypeSymbol sharedTextureSlotSymbol,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        using ImmutableArrayBuilder<IFieldSymbol> slotBuilder = new();

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
        {
            if (memberSymbol is IFieldSymbol { Type: INamedTypeSymbol slotTypeSymbol } fieldSymbol &&
                memberSymbol.HasAttributeWithType(sharedTextureAttributeSymbol) &&
                SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, sharedTextureSlotSymbol))
            {
                slotBuilder.Add(fieldSymbol);
            }
        }

        Dictionary<string, int> canonicalNameCounts = GetCanonicalNameCounts(slotBuilder.WrittenSpan);

        foreach (IFieldSymbol fieldSymbol in slotBuilder.WrittenSpan)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(fieldSymbol.MetadataName, out string canonicalName) ||
                canonicalNameCounts[canonicalName] > 1 ||
                IsGeneratedSharedTextureMemberDeclared(typeSymbol, canonicalName, generatedCodeAttributeSymbol))
            {
                Report(context, fieldSymbol, typeSymbol);
            }
        }
    }

    /// <summary>
    /// Analyzes the members of a resource group.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="typeSymbol">The resource group type.</param>
    /// <param name="resourceAttributeSymbol">The <c>[ComputePipelineResource]</c> symbol.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    private static void AnalyzeResourceGroup(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol resourceAttributeSymbol,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        using ImmutableArrayBuilder<IPropertySymbol> memberBuilder = new();

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
        {
            if (memberSymbol is IPropertySymbol propertySymbol && memberSymbol.HasAttributeWithType(resourceAttributeSymbol))
            {
                memberBuilder.Add(propertySymbol);
            }
        }

        if (memberBuilder.Count == 0)
        {
            return;
        }

        Dictionary<string, int> canonicalNameCounts = GetCanonicalNameCounts(memberBuilder.WrittenSpan);

        bool isPlanTypeNameDeclared = GeneratedMemberLookup.IsDeclaredByUser(
            typeSymbol,
            GeneratedIdentifier.ResourceGroupPlanTypeName,
            generatedCodeAttributeSymbol);

        foreach (IPropertySymbol propertySymbol in memberBuilder.WrittenSpan)
        {
            if (isPlanTypeNameDeclared ||
                !GeneratedIdentifier.TryCreateCanonicalName(propertySymbol.MetadataName, out string canonicalName) ||
                canonicalNameCounts[canonicalName] > 1)
            {
                Report(context, propertySymbol, typeSymbol);
            }
        }
    }

    /// <summary>
    /// Gets the number of owned members producing each generated canonical name.
    /// </summary>
    /// <typeparam name="T">The type of the owned members.</typeparam>
    /// <param name="memberSymbols">The owned members to count the canonical names of.</param>
    /// <returns>The number of owned members producing each generated canonical name.</returns>
    private static Dictionary<string, int> GetCanonicalNameCounts<T>(ReadOnlySpan<T> memberSymbols)
        where T : ISymbol
    {
        Dictionary<string, int> canonicalNameCounts = [];

        foreach (T memberSymbol in memberSymbols)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(memberSymbol.MetadataName, out string canonicalName))
            {
                continue;
            }

            canonicalNameCounts[canonicalName] = canonicalNameCounts.TryGetValue(canonicalName, out int count) ? count + 1 : 1;
        }

        return canonicalNameCounts;
    }

    /// <summary>
    /// Checks whether an owned slot field declares a resource group slot.
    /// </summary>
    /// <param name="fieldSymbol">The owned slot field.</param>
    /// <param name="resourceGroupSlotSymbol">The <c>ComputeResourceGroupSlot&lt;TGroup&gt;</c> symbol.</param>
    /// <returns>Whether <paramref name="fieldSymbol"/> declares a resource group slot.</returns>
    private static bool IsResourceGroupSlot(IFieldSymbol fieldSymbol, INamedTypeSymbol resourceGroupSlotSymbol)
    {
        return fieldSymbol.Type is INamedTypeSymbol slotTypeSymbol &&
               SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, resourceGroupSlotSymbol);
    }

    /// <summary>
    /// Checks whether a compute pipeline host declares a member generated for a given canonical name.
    /// </summary>
    /// <param name="typeSymbol">The compute pipeline host type.</param>
    /// <param name="canonicalName">The generated canonical name of the owned slot.</param>
    /// <param name="isResourceGroupSlot">Whether the owned slot declares a resource group slot.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> declares a member generated for <paramref name="canonicalName"/>.</returns>
    private static bool IsGeneratedSlotMemberDeclared(
        INamedTypeSymbol typeSymbol,
        string canonicalName,
        bool isResourceGroupSlot,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        if (GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, GeneratedIdentifier.CreateMaterializerTypeName(canonicalName), generatedCodeAttributeSymbol) ||
            GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"TryEnsure{canonicalName}", generatedCodeAttributeSymbol))
        {
            return true;
        }

        if (isResourceGroupSlot)
        {
            return false;
        }

        return GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, GeneratedIdentifier.CreatePlanTypeName(canonicalName), generatedCodeAttributeSymbol) ||
               GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"Get{canonicalName}ComputeBinding", generatedCodeAttributeSymbol);
    }

    /// <summary>
    /// Checks whether a compute interop resource set declares a member generated for a given canonical name.
    /// </summary>
    /// <param name="typeSymbol">The compute interop resource set type.</param>
    /// <param name="canonicalName">The generated canonical name of the owned slot.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> declares a member generated for <paramref name="canonicalName"/>.</returns>
    private static bool IsGeneratedSharedTextureMemberDeclared(
        INamedTypeSymbol typeSymbol,
        string canonicalName,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        return GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"TryEnsure{canonicalName}", generatedCodeAttributeSymbol) ||
               GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"TryGet{canonicalName}AllocatedSize", generatedCodeAttributeSymbol) ||
               GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"Get{canonicalName}ComputeBinding", generatedCodeAttributeSymbol) ||
               GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"Begin{canonicalName}ExternalOperation", generatedCodeAttributeSymbol) ||
               GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, $"Acquire{canonicalName}ExternalViewLease", generatedCodeAttributeSymbol);
    }

    /// <summary>
    /// Reports a generated plan signature conflict for a given owned member.
    /// </summary>
    /// <param name="context">The current symbol analysis context.</param>
    /// <param name="memberSymbol">The owned member the conflict was found for.</param>
    /// <param name="typeSymbol">The type declaring the owned member.</param>
    private static void Report(SymbolAnalysisContext context, ISymbol memberSymbol, INamedTypeSymbol typeSymbol)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            InvalidGeneratedPlanSignature,
            memberSymbol.Locations[0],
            memberSymbol,
            typeSymbol));
    }
}
