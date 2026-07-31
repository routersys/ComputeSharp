using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever the members generated for a pipeline method conflict with a declared member.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidGeneratedPipelineOverloadAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [InvalidGeneratedPipelineOverload];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the symbols the pipeline methods of a host are declared with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } hostAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute") is not { } generatedCodeAttributeSymbol)
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

                using ImmutableArrayBuilder<IMethodSymbol> pipelineBuilder = new();

                foreach (ISymbol memberSymbol in typeSymbol.GetMembers())
                {
                    if (memberSymbol is IMethodSymbol methodSymbol && memberSymbol.HasAttributeWithType(pipelineAttributeSymbol))
                    {
                        pipelineBuilder.Add(methodSymbol);
                    }
                }

                if (pipelineBuilder.Count == 0)
                {
                    return;
                }

                Dictionary<string, int> invocationTypeNameCounts = GetInvocationTypeNameCounts(pipelineBuilder.WrittenSpan);

                foreach (IMethodSymbol methodSymbol in pipelineBuilder.WrittenSpan)
                {
                    if (!GeneratedIdentifier.TryCreateCanonicalName(methodSymbol.MetadataName, out string canonicalName) ||
                        invocationTypeNameCounts[GeneratedIdentifier.CreateInvocationTypeName(canonicalName)] > 1 ||
                        GeneratedMemberLookup.IsDeclaredByUser(typeSymbol, GeneratedIdentifier.CreateInvocationTypeName(canonicalName), generatedCodeAttributeSymbol) ||
                        IsGeneratedOverloadDeclared(typeSymbol, methodSymbol, pipelineAttributeSymbol, generatedCodeAttributeSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidGeneratedPipelineOverload,
                            methodSymbol.Locations[0],
                            methodSymbol,
                            typeSymbol));
                    }
                }
            }, SymbolKind.NamedType);
        });
    }

    /// <summary>
    /// Gets the number of pipeline methods producing each generated invocation type name.
    /// </summary>
    /// <param name="methodSymbols">The pipeline methods to count the generated invocation type names of.</param>
    /// <returns>The number of pipeline methods producing each generated invocation type name.</returns>
    private static Dictionary<string, int> GetInvocationTypeNameCounts(ReadOnlySpan<IMethodSymbol> methodSymbols)
    {
        Dictionary<string, int> invocationTypeNameCounts = [];

        foreach (IMethodSymbol methodSymbol in methodSymbols)
        {
            if (!GeneratedIdentifier.TryCreateCanonicalName(methodSymbol.MetadataName, out string canonicalName))
            {
                continue;
            }

            string invocationTypeName = GeneratedIdentifier.CreateInvocationTypeName(canonicalName);

            invocationTypeNameCounts[invocationTypeName] = invocationTypeNameCounts.TryGetValue(invocationTypeName, out int count) ? count + 1 : 1;
        }

        return invocationTypeNameCounts;
    }

    /// <summary>
    /// Checks whether a compute pipeline host declares a member conflicting with the overload generated for a pipeline method.
    /// </summary>
    /// <param name="typeSymbol">The compute pipeline host type.</param>
    /// <param name="methodSymbol">The pipeline method the overload is generated for.</param>
    /// <param name="pipelineAttributeSymbol">The <c>[ComputePipeline]</c> symbol.</param>
    /// <param name="generatedCodeAttributeSymbol">The <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> symbol.</param>
    /// <returns>Whether the overload generated for <paramref name="methodSymbol"/> is already declared.</returns>
    private static bool IsGeneratedOverloadDeclared(
        INamedTypeSymbol typeSymbol,
        IMethodSymbol methodSymbol,
        INamedTypeSymbol pipelineAttributeSymbol,
        INamedTypeSymbol generatedCodeAttributeSymbol)
    {
        foreach (ISymbol memberSymbol in typeSymbol.GetMembers(methodSymbol.Name))
        {
            // The generated overload drops the context parameter, so a pipeline method never conflicts with
            // one generated for another pipeline method. Generated members are the ones being validated here
            if (memberSymbol is not IMethodSymbol { IsGenericMethod: false } candidateSymbol ||
                memberSymbol.HasAttributeWithType(pipelineAttributeSymbol) ||
                memberSymbol.HasAttributeWithType(generatedCodeAttributeSymbol))
            {
                continue;
            }

            if (HasGeneratedOverloadSignature(methodSymbol, candidateSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a declared method has the signature of the overload generated for a pipeline method.
    /// </summary>
    /// <param name="methodSymbol">The pipeline method the overload is generated for.</param>
    /// <param name="candidateSymbol">The declared method to compare against.</param>
    /// <returns>Whether <paramref name="candidateSymbol"/> has the signature of the generated overload.</returns>
    private static bool HasGeneratedOverloadSignature(IMethodSymbol methodSymbol, IMethodSymbol candidateSymbol)
    {
        // The generated overload takes every parameter but the leading context one. Parameter modifiers are
        // not compared, as C# does not allow two overloads that differ only by them
        if (candidateSymbol.Parameters.Length != methodSymbol.Parameters.Length - 1)
        {
            return false;
        }

        for (int i = 0; i < candidateSymbol.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(candidateSymbol.Parameters[i].Type, methodSymbol.Parameters[i + 1].Type))
            {
                return false;
            }
        }

        return true;
    }
}
