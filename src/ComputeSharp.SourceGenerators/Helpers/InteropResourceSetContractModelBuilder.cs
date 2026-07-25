using System.Collections.Generic;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The builder of the final immutable contract model of a compute interop resource set.
/// </summary>
internal static class InteropResourceSetContractModelBuilder
{
    /// <summary>
    /// The fully qualified metadata name of the shared texture compute binding type definition.
    /// </summary>
    private const string SharedTextureBindingTypeMetadataName = "ComputeSharp.ReadWriteTexture2D`2";

    /// <summary>
    /// Tries to build the final contract model of a given interop resource set.
    /// </summary>
    /// <param name="resourceSetSymbol">The resource set type to build the contract model of.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="resourceSet">The resulting contract model, if every declaration is valid.</param>
    /// <returns>Whether the contract model of <paramref name="resourceSetSymbol"/> could be built.</returns>
    public static bool TryBuild(
        INamedTypeSymbol resourceSetSymbol,
        PipelineWellKnownSymbols symbols,
        out InteropResourceSetContractInfo resourceSet)
    {
        resourceSet = null!;

        if (!resourceSetSymbol.HasAttributeWithType(symbols.InteropResourceSetAttribute))
        {
            return false;
        }

        using ImmutableArrayBuilder<SharedTextureContractInfo> builder = new();

        HashSet<string> canonicalNames = [];

        foreach (ISymbol memberSymbol in resourceSetSymbol.GetMembers())
        {
            if (!memberSymbol.TryGetAttributeWithType(symbols.SharedTextureAttribute, out AttributeData? attribute))
            {
                continue;
            }

            if (memberSymbol is not IFieldSymbol { DeclaredAccessibility: Accessibility.Private, IsReadOnly: true, IsStatic: false } fieldSymbol ||
                HasInitializer(fieldSymbol) ||
                !GeneratedIdentifier.TryCreateCanonicalName(fieldSymbol.MetadataName, out string canonicalName) ||
                !canonicalNames.Add(canonicalName) ||
                !TryGetSharedTextureBindingType(fieldSymbol.Type, symbols, out string? bindingTypeMetadataName) ||
                !TryGetSharedTextureContract(attribute, out SharedTextureAttributeValues values))
            {
                return false;
            }

            builder.Add(new SharedTextureContractInfo(
                uint.MaxValue,
                fieldSymbol.MetadataName,
                bindingTypeMetadataName,
                values.ResizePolicy,
                values.ComputeAccess,
                values.ExternalAccess,
                values.ExternalUsage,
                values.AlphaMode,
                values.InitialOwner,
                values.Recovery));
        }

        if (builder.Count == 0)
        {
            return false;
        }

        EquatableArray<SharedTextureContractInfo> sharedTextures = PipelineCanonicalOrdering.OrderSharedTextures(builder.ToImmutable());

        resourceSet = new InteropResourceSetContractInfo(
            CanonicalTypeNameBuilder.GetCanonicalTypeName(resourceSetSymbol),
            sharedTextures.Length,
            sharedTextures);

        return PipelineContractLimitValidator.IsWithinLimits(resourceSet);
    }

    /// <summary>
    /// Tries to get the canonical metadata name of the compute binding type of a shared texture slot.
    /// </summary>
    /// <param name="slotTypeSymbol">The declared shared texture slot type.</param>
    /// <param name="symbols">The well known symbols to resolve the declaration with.</param>
    /// <param name="bindingTypeMetadataName">The resulting canonical metadata name of the compute binding type.</param>
    /// <returns>Whether the compute binding type could be resolved.</returns>
    private static bool TryGetSharedTextureBindingType(
        ITypeSymbol slotTypeSymbol,
        PipelineWellKnownSymbols symbols,
        out string bindingTypeMetadataName)
    {
        bindingTypeMetadataName = "";

        if (slotTypeSymbol is not INamedTypeSymbol { IsGenericType: true } namedTypeSymbol ||
            !SymbolEqualityComparer.Default.Equals(namedTypeSymbol.OriginalDefinition, symbols.SharedTextureSlot))
        {
            return false;
        }

        using ImmutableArrayBuilder<char> builder = new();

        builder.AddRange(SharedTextureBindingTypeMetadataName.ToCharArray());
        builder.Add('[');

        CanonicalTypeNameBuilder.AppendCanonicalTypeName(namedTypeSymbol.TypeArguments[0], in builder);

        builder.Add(',');

        CanonicalTypeNameBuilder.AppendCanonicalTypeName(namedTypeSymbol.TypeArguments[1], in builder);

        builder.Add(']');

        bindingTypeMetadataName = builder.ToString();

        return true;
    }

    /// <summary>
    /// Tries to get the declared contract values of a <c>[ComputeSharedTexture]</c> attribute.
    /// </summary>
    /// <param name="attribute">The attribute data to read.</param>
    /// <param name="values">The resulting contract values, if the attribute declares a supported contract.</param>
    /// <returns>Whether the attribute declares a supported contract.</returns>
    private static bool TryGetSharedTextureContract(AttributeData attribute, out SharedTextureAttributeValues values)
    {
        values = default;

        if (attribute.ConstructorArguments is not [{ Value: byte resizePolicy }, { Value: byte computeAccess }, { Value: byte externalAccess }, { Value: byte externalUsage }, { Value: byte alphaMode }, { Value: byte initialOwner }, { Value: byte recovery }])
        {
            return false;
        }

        values = new SharedTextureAttributeValues(
            (ComputeResourceResizePolicy)resizePolicy,
            (ComputeResourceAccess)computeAccess,
            (ExternalResourceAccess)externalAccess,
            (ExternalTextureUsage)externalUsage,
            (ComputeAlphaMode)alphaMode,
            (ComputeSharedTextureInitialOwner)initialOwner,
            (ComputeResourceRecovery)recovery);

        return values.ResizePolicy is ComputeResourceResizePolicy.Exact or ComputeResourceResizePolicy.GrowOnly &&
            values.ComputeAccess is ComputeResourceAccess.ReadWrite &&
            values.ExternalAccess is ExternalResourceAccess.Read or ExternalResourceAccess.Write or ExternalResourceAccess.ReadWrite &&
            values.ExternalUsage is ExternalTextureUsage.Sampled or ExternalTextureUsage.RenderTarget &&
            values.AlphaMode is ComputeAlphaMode.Ignore or ComputeAlphaMode.Premultiplied or ComputeAlphaMode.Straight &&
            values.InitialOwner is ComputeSharedTextureInitialOwner.Compute or ComputeSharedTextureInitialOwner.External &&
            values.Recovery is ComputeResourceRecovery.Discardable or ComputeResourceRecovery.RecreateFromHost or ComputeResourceRecovery.Recompute or ComputeResourceRecovery.CapacityOnly;
    }

    /// <summary>
    /// Checks whether a given field is initialized in its declaration.
    /// </summary>
    /// <param name="fieldSymbol">The field to check.</param>
    /// <returns>Whether <paramref name="fieldSymbol"/> is initialized in its declaration.</returns>
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

    /// <summary>
    /// The declared contract values of a <c>[ComputeSharedTexture]</c> attribute.
    /// </summary>
    /// <param name="ResizePolicy">The resize policy of the shared texture.</param>
    /// <param name="ComputeAccess">The compute access of the shared texture.</param>
    /// <param name="ExternalAccess">The external access of the shared texture.</param>
    /// <param name="ExternalUsage">The external usage of the shared texture.</param>
    /// <param name="AlphaMode">The alpha mode of the shared texture.</param>
    /// <param name="InitialOwner">The initial owner of the shared texture.</param>
    /// <param name="Recovery">The recovery class of the shared texture.</param>
    private readonly record struct SharedTextureAttributeValues(
        ComputeResourceResizePolicy ResizePolicy,
        ComputeResourceAccess ComputeAccess,
        ExternalResourceAccess ExternalAccess,
        ExternalTextureUsage ExternalUsage,
        ComputeAlphaMode AlphaMode,
        ComputeSharedTextureInitialOwner InitialOwner,
        ComputeResourceRecovery Recovery);
}
