using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGenerators.Helpers;

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
            if (!methodSymbols.TryGetValue(pipeline.MethodMetadataName, out IMethodSymbol? methodSymbol) ||
                !TryBuildInvocation(methodSymbol, symbols, pipeline, host.Slots, resources, internalBuilder.WrittenSpan, out PipelineInvocationSyntaxInfo invocation))
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
    /// <param name="slots">The owned slots of the host, in canonical slot ordinal order.</param>
    /// <param name="resources">The internal resource contracts of the host, in canonical member order.</param>
    /// <param name="internalBindings">The pins of the internal resources, in canonical member order.</param>
    /// <param name="invocation">The resulting invocation, if its members could be built.</param>
    /// <returns>Whether the members of <paramref name="methodSymbol"/> could be built.</returns>
    private static bool TryBuildInvocation(
        IMethodSymbol methodSymbol,
        PipelineWellKnownSymbols symbols,
        PipelineContractInfo pipeline,
        EquatableArray<OwnedSlotContractInfo> slots,
        EquatableArray<UnorderedInternalResourceContract> resources,
        ReadOnlySpan<PipelineBindingSyntaxInfo> internalBindings,
        out PipelineInvocationSyntaxInfo invocation)
    {
        invocation = null!;

        if (!GeneratedIdentifier.TryCreateCanonicalName(methodSymbol.MetadataName, out string canonicalName))
        {
            return false;
        }

        using ImmutableArrayBuilder<PipelineParameterSyntaxInfo> parameterBuilder = new();
        using ImmutableArrayBuilder<PipelineArgumentSyntaxInfo> argumentBuilder = new();
        using ImmutableArrayBuilder<PipelineBindingSyntaxInfo> bindingBuilder = new();
        using ImmutableArrayBuilder<PipelineOwnedResourceSyntaxInfo> ownedBuilder = new();

        Accessibility accessibility = Accessibility.Public;

        for (int i = 1; i < methodSymbol.Parameters.Length; i++)
        {
            IParameterSymbol parameterSymbol = methodSymbol.Parameters[i];
            bool isReadOnlyReference = parameterSymbol.RefKind is RefKind.In;

            if (parameterSymbol.TryGetAttributeWithType(symbols.OwnedResourceAttribute, out AttributeData? ownedAttribute))
            {
                if (parameterSymbol.HasAttributeWithType(symbols.ResourceAttribute) ||
                    !TryBuildOwnedResource(parameterSymbol, ownedAttribute, slots, resources, out PipelineOwnedResourceSyntaxInfo owned))
                {
                    return false;
                }

                argumentBuilder.Add(new PipelineArgumentSyntaxInfo(
                    PipelineArgumentKind.OwnedResource,
                    parameterSymbol.Name,
                    isReadOnlyReference));

                ownedBuilder.Add(owned);

                continue;
            }

            string typeName = parameterSymbol.Type.GetFullyQualifiedName(includeGlobal: true);
            bool isExternal = PipelineCollector.IsExternalResource(parameterSymbol, symbols.ResourceAttribute);

            parameterBuilder.Add(new PipelineParameterSyntaxInfo(
                isExternal ? $"global::ComputeWeave.ComputeResourceBinding<{typeName}>" : typeName,
                parameterSymbol.Name,
                isReadOnlyReference,
                isExternal ? typeName : null));

            argumentBuilder.Add(new PipelineArgumentSyntaxInfo(
                isExternal ? PipelineArgumentKind.ExternalResource : PipelineArgumentKind.Parameter,
                parameterSymbol.Name,
                isReadOnlyReference));

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
                isExternal ? PipelineBindingKind.ExternalParameter : PipelineBindingKind.Parameter,
                parameterSymbol.Name,
                typeName,
                0,
                0));
        }

        if (bindingBuilder.Count != pipeline.Parameters.Length)
        {
            return false;
        }

        EquatableArray<PipelineOwnedResourceSyntaxInfo> ownedResources = OffsetBindingIndices(ownedBuilder.WrittenSpan, bindingBuilder.Count);

        AddInternalBindings(in bindingBuilder, internalBindings, ownedResources);

        invocation = new PipelineInvocationSyntaxInfo(
            pipeline.Ordinal,
            methodSymbol.MetadataName,
            GeneratedIdentifier.CreateInvocationTypeName(canonicalName),
            GeneratedAccessibility.GetKeyword(accessibility),
            parameterBuilder.ToImmutable(),
            bindingBuilder.ToImmutable(),
            argumentBuilder.ToImmutable(),
            ownedResources);

        return true;
    }

    /// <summary>
    /// Tries to build the model of a single owned resource parameter.
    /// </summary>
    /// <param name="parameterSymbol">The annotated pipeline method parameter.</param>
    /// <param name="attribute">The <c>[ComputeOwnedResource]</c> data of the parameter.</param>
    /// <param name="slots">The owned slots of the host, in canonical slot ordinal order.</param>
    /// <param name="resources">The internal resource contracts of the host, in canonical member order.</param>
    /// <param name="owned">The resulting model, with indices relative to the internal resource contracts.</param>
    /// <returns>Whether the model of <paramref name="parameterSymbol"/> could be built.</returns>
    private static bool TryBuildOwnedResource(
        IParameterSymbol parameterSymbol,
        AttributeData attribute,
        EquatableArray<OwnedSlotContractInfo> slots,
        EquatableArray<UnorderedInternalResourceContract> resources,
        out PipelineOwnedResourceSyntaxInfo owned)
    {
        owned = null!;

        if (attribute.ConstructorArguments is not [{ Value: string slotFieldName }] ||
            !TryGetSlot(slots, slotFieldName, out OwnedSlotContractInfo? slot) ||
            !GeneratedIdentifier.TryCreateCanonicalName(slot.MemberMetadataName, out string canonicalName) ||
            parameterSymbol.Type.GetFullyQualifiedName(includeGlobal: true) != slot.ResourceTypeName)
        {
            return false;
        }

        using ImmutableArrayBuilder<int> indexBuilder = new();

        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i].SlotKey?.HostMemberMetadataName == slotFieldName)
            {
                indexBuilder.Add(i);
            }
        }

        if (indexBuilder.Count != slot.Resources.Length)
        {
            return false;
        }

        owned = new PipelineOwnedResourceSyntaxInfo(
            parameterSymbol.Name,
            slot.ResourceTypeName,
            slot.Ownership is ResourceOwnershipKind.OwnedGroupSlot ? GeneratedIdentifier.CreateGenerationFieldName(canonicalName) : null,
            indexBuilder.ToImmutable());

        return true;
    }

    /// <summary>
    /// Tries to get the owned slot declared by a given host field.
    /// </summary>
    /// <param name="slots">The owned slots of the host, in canonical slot ordinal order.</param>
    /// <param name="slotFieldName">The metadata name of the host field declaring the slot.</param>
    /// <param name="slot">The resulting owned slot, if one is declared by <paramref name="slotFieldName"/>.</param>
    /// <returns>Whether <paramref name="slotFieldName"/> declares an owned slot.</returns>
    private static bool TryGetSlot(
        EquatableArray<OwnedSlotContractInfo> slots,
        string slotFieldName,
        [NotNullWhen(true)] out OwnedSlotContractInfo? slot)
    {
        foreach (OwnedSlotContractInfo candidate in slots)
        {
            if (candidate.MemberMetadataName == slotFieldName)
            {
                slot = candidate;

                return true;
            }
        }

        slot = null;

        return false;
    }

    /// <summary>
    /// Rebases the pin indices of a set of owned resource parameters onto the pins of a pipeline.
    /// </summary>
    /// <param name="ownedResources">The owned resource parameters, with indices relative to the internal resource contracts.</param>
    /// <param name="offset">The number of pins preceding the internal resources.</param>
    /// <returns>The resulting owned resource parameters, with indices relative to the pins of the pipeline.</returns>
    private static EquatableArray<PipelineOwnedResourceSyntaxInfo> OffsetBindingIndices(
        ReadOnlySpan<PipelineOwnedResourceSyntaxInfo> ownedResources,
        int offset)
    {
        using ImmutableArrayBuilder<PipelineOwnedResourceSyntaxInfo> builder = new();

        foreach (PipelineOwnedResourceSyntaxInfo owned in ownedResources)
        {
            using ImmutableArrayBuilder<int> indexBuilder = new();

            foreach (int index in owned.BindingIndices)
            {
                indexBuilder.Add(index + offset);
            }

            builder.Add(owned with { BindingIndices = indexBuilder.ToImmutable() });
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Adds the pins of the internal resources of a host to those of a pipeline, marking the ones it resolves.
    /// </summary>
    /// <param name="bindingBuilder">The target pin builder.</param>
    /// <param name="internalBindings">The pins of the internal resources, in canonical member order.</param>
    /// <param name="ownedResources">The owned resource parameters of the pipeline.</param>
    private static void AddInternalBindings(
        ref readonly ImmutableArrayBuilder<PipelineBindingSyntaxInfo> bindingBuilder,
        ReadOnlySpan<PipelineBindingSyntaxInfo> internalBindings,
        EquatableArray<PipelineOwnedResourceSyntaxInfo> ownedResources)
    {
        int offset = bindingBuilder.Count;

        HashSet<int> resolvedIndices = [];

        foreach (PipelineOwnedResourceSyntaxInfo owned in ownedResources)
        {
            foreach (int index in owned.BindingIndices)
            {
                _ = resolvedIndices.Add(index);
            }
        }

        for (int i = 0; i < internalBindings.Length; i++)
        {
            bindingBuilder.Add(internalBindings[i] with { IsResolved = resolvedIndices.Contains(offset + i) });
        }
    }
}
