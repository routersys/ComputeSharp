using System.Collections.Immutable;
using System.Linq;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a compute pipeline container attribute is used on an invalid target type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidComputePipelineContainerTypeDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        InvalidComputePipelineHostType,
        InvalidComputePipelineHostMaximumConcurrentInvocations,
        InvalidComputeInteropResourceSetType,
        InvalidComputeResourceGroupType,
        InvalidComputePipelineContainerType
    ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [ComputePipelineHost], [ComputeInteropResourceSet] and [ComputeResourceGroup] symbols
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeInteropResourceSetAttribute") is not { } resourceSetAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupAttribute") is not { } resourceGroupAttributeSymbol)
            {
                return;
            }

            context.RegisterSymbolAction(context =>
            {
                if (context.Symbol is not INamedTypeSymbol typeSymbol)
                {
                    return;
                }

                // Resolve which container attribute is present, along with the matching declaration diagnostic
                DiagnosticDescriptor declarationDescriptor;
                string attributeName;
                AttributeData attribute;
                bool isHost = false;

                if (typeSymbol.TryGetAttributeWithType(hostAttributeSymbol, out AttributeData? hostAttribute))
                {
                    declarationDescriptor = InvalidComputePipelineHostType;
                    attributeName = "ComputePipelineHost";
                    attribute = hostAttribute;
                    isHost = true;
                }
                else if (typeSymbol.TryGetAttributeWithType(resourceSetAttributeSymbol, out AttributeData? resourceSetAttribute))
                {
                    declarationDescriptor = InvalidComputeInteropResourceSetType;
                    attributeName = "ComputeInteropResourceSet";
                    attribute = resourceSetAttribute;
                }
                else if (typeSymbol.TryGetAttributeWithType(resourceGroupAttributeSymbol, out AttributeData? resourceGroupAttribute))
                {
                    declarationDescriptor = InvalidComputeResourceGroupType;
                    attributeName = "ComputeResourceGroup";
                    attribute = resourceGroupAttribute;
                }
                else
                {
                    return;
                }

                // The type must be a sealed partial class
                if (typeSymbol.TypeKind is not TypeKind.Class ||
                    !typeSymbol.IsSealed ||
                    !IsPartial(typeSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        declarationDescriptor,
                        attribute.GetLocation(),
                        typeSymbol));
                }

                // The type cannot be generic and all its containing types must be partial
                if (typeSymbol.IsGenericType ||
                    !AreAllContainingTypesPartial(typeSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputePipelineContainerType,
                        attribute.GetLocation(),
                        typeSymbol,
                        attributeName));
                }

                // The host attribute must specify a maximum concurrent invocations value greater than or equal to 1
                if (isHost &&
                    attribute.ConstructorArguments is [_, { Value: int maximumConcurrentInvocations }] &&
                    maximumConcurrentInvocations < 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidComputePipelineHostMaximumConcurrentInvocations,
                        attribute.GetLocation(),
                        typeSymbol));
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Checks whether a given type is declared as partial.
    /// </summary>
    /// <param name="typeSymbol">The input type to check.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> is declared as partial.</returns>
    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        return
            typeSymbol.DeclaringSyntaxReferences.Length > 0 &&
            typeSymbol.DeclaringSyntaxReferences.All(static reference =>
                reference.GetSyntax() is TypeDeclarationSyntax typeDeclaration &&
                typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    /// <summary>
    /// Checks whether all containing types of a given type are declared as partial.
    /// </summary>
    /// <param name="typeSymbol">The input type to check.</param>
    /// <returns>Whether all containing types of <paramref name="typeSymbol"/> are declared as partial.</returns>
    private static bool AreAllContainingTypesPartial(INamedTypeSymbol typeSymbol)
    {
        for (INamedTypeSymbol? containingType = typeSymbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            if (!IsPartial(containingType))
            {
                return false;
            }
        }

        return true;
    }
}
