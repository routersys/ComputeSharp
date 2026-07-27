using System;
using System.Collections.Generic;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the members generated for the pipeline methods of a compute pipeline host.
/// </summary>
internal static class PipelineInvocationSyntaxModelBuilder
{
    /// <summary>
    /// Tries to build the members generated for the pipeline methods of a given host.
    /// </summary>
    /// <param name="hostSymbol">The host type declaring the pipeline methods.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="host">The contract model of the host.</param>
    /// <param name="resources">The internal resource contracts of the host, in canonical member order.</param>
    /// <param name="invocations">The resulting invocations, in canonical pipeline ordinal order.</param>
    /// <returns>Whether the members of every pipeline of <paramref name="hostSymbol"/> could be built.</returns>
    public static bool TryBuild(
        INamedTypeSymbol hostSymbol,
        PipelineWellKnownSymbols symbols,
        PipelineHostContractInfo host,
        EquatableArray<UnorderedInternalResourceContract> resources,
        out EquatableArray<PipelineInvocationSyntaxInfo> invocations)
    {
        Dictionary<string, IMethodSymbol> methodSymbols = [];

        foreach (ISymbol memberSymbol in hostSymbol.GetMembers())
        {
            if (memberSymbol is IMethodSymbol methodSymbol &&
                methodSymbol.HasAttributeWithType(symbols.PipelineAttribute) &&
                !methodSymbols.ContainsKey(methodSymbol.MetadataName))
            {
                methodSymbols.Add(methodSymbol.MetadataName, methodSymbol);
            }
        }

        using ImmutableArrayBuilder<PipelineBindingSyntaxInfo> internalBuilder = new();

        foreach (UnorderedInternalResourceContract resource in resources)
        {
            internalBuilder.Add(CreateInternalBinding(resource, host.Slots));
        }

        using ImmutableArrayBuilder<PipelineInvocationSyntaxInfo> builder = new();

        foreach (PipelineContractInfo pipeline in host.Pipelines)
        {
            if (pipeline.Flags is PipelineFlags.InteropRoundTrip)
            {
                continue;
            }

            if (!methodSymbols.TryGetValue(pipeline.MethodMetadataName, out IMethodSymbol? methodSymbol) ||
                !TryBuildInvocation(methodSymbol, symbols, pipeline, internalBuilder.WrittenSpan, out PipelineInvocationSyntaxInfo invocation))
            {
                invocations = default;

                return false;
            }

            builder.Add(invocation);
        }

        invocations = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Creates the pin of a single internal resource contract.
    /// </summary>
    /// <param name="resource">The internal resource contract to create the pin of.</param>
    /// <param name="slots">The owned slots of the host, in canonical slot ordinal order.</param>
    /// <returns>The pin of <paramref name="resource"/>.</returns>
    private static PipelineBindingSyntaxInfo CreateInternalBinding(
        UnorderedInternalResourceContract resource,
        EquatableArray<OwnedSlotContractInfo> slots)
    {
        if (resource.SlotKey is not { } slotKey)
        {
            return new PipelineBindingSyntaxInfo(
                PipelineBindingKind.BorrowedField,
                resource.HostMemberMetadataName,
                resource.ResourceTypeName,
                0,
                0);
        }

        uint slotOrdinal = 0;

        foreach (OwnedSlotContractInfo slot in slots)
        {
            if (slot.MemberMetadataName == slotKey.HostMemberMetadataName)
            {
                slotOrdinal = slot.Ordinal;

                break;
            }
        }

        return new PipelineBindingSyntaxInfo(
            PipelineBindingKind.OwnedSlot,
            resource.HostMemberMetadataName,
            resource.ResourceTypeName,
            slotOrdinal,
            resource.SlotResourceIndex);
    }

    /// <summary>
    /// Tries to build the members generated for a single pipeline method.
    /// </summary>
    /// <param name="methodSymbol">The declared pipeline method.</param>
    /// <param name="symbols">The well known symbols to resolve the declaration with.</param>
    /// <param name="pipeline">The contract model of the pipeline.</param>
    /// <param name="internalBindings">The pins of the internal resources, in canonical member order.</param>
    /// <param name="invocation">The resulting invocation, if its members could be built.</param>
    /// <returns>Whether the members of <paramref name="methodSymbol"/> could be built.</returns>
    private static bool TryBuildInvocation(
        IMethodSymbol methodSymbol,
        PipelineWellKnownSymbols symbols,
        PipelineContractInfo pipeline,
        ReadOnlySpan<PipelineBindingSyntaxInfo> internalBindings,
        out PipelineInvocationSyntaxInfo invocation)
    {
        invocation = null!;

        if (!GeneratedIdentifier.TryCreateCanonicalName(methodSymbol.MetadataName, out string canonicalName))
        {
            return false;
        }

        using ImmutableArrayBuilder<PipelineParameterSyntaxInfo> parameterBuilder = new();
        using ImmutableArrayBuilder<PipelineBindingSyntaxInfo> bindingBuilder = new();

        Accessibility accessibility = Accessibility.Public;

        for (int i = 1; i < methodSymbol.Parameters.Length; i++)
        {
            IParameterSymbol parameterSymbol = methodSymbol.Parameters[i];
            string typeName = parameterSymbol.Type.GetFullyQualifiedName(includeGlobal: true);

            parameterBuilder.Add(new PipelineParameterSyntaxInfo(
                typeName,
                parameterSymbol.Name,
                parameterSymbol.RefKind is RefKind.In));

            if (GeneratedAccessibility.GetEffectiveAccessibility(parameterSymbol.Type) is Accessibility parameterAccessibility &&
                parameterAccessibility < accessibility)
            {
                accessibility = parameterAccessibility;
            }

            if (!parameterSymbol.HasAttributeWithType(symbols.ResourceAttribute))
            {
                continue;
            }

            bindingBuilder.Add(new PipelineBindingSyntaxInfo(
                PipelineBindingKind.Parameter,
                parameterSymbol.Name,
                typeName,
                0,
                0));
        }

        if (bindingBuilder.Count != pipeline.Parameters.Length)
        {
            return false;
        }

        bindingBuilder.AddRange(internalBindings);

        invocation = new PipelineInvocationSyntaxInfo(
            pipeline.Ordinal,
            methodSymbol.MetadataName,
            GeneratedIdentifier.CreateInvocationTypeName(canonicalName),
            GeneratedAccessibility.GetKeyword(accessibility),
            parameterBuilder.ToImmutable(),
            bindingBuilder.ToImmutable());

        return true;
    }
}
