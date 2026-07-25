using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The collection rules for the pipelines declared by a compute pipeline host.
/// </summary>
internal static class PipelineCollector
{
    /// <summary>
    /// Tries to collect the pipelines declared by a given host.
    /// </summary>
    /// <param name="hostSymbol">The host type to collect the pipelines of.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="pipelines">The resulting pipelines, without their ordinals and structural requirements.</param>
    /// <returns>Whether every pipeline of <paramref name="hostSymbol"/> could be collected.</returns>
    public static bool TryCollect(
        INamedTypeSymbol hostSymbol,
        PipelineWellKnownSymbols symbols,
        out EquatableArray<PipelineContractInfo> pipelines)
    {
        using ImmutableArrayBuilder<PipelineContractInfo> builder = new();

        foreach (ISymbol memberSymbol in hostSymbol.GetMembers())
        {
            if (memberSymbol is not IMethodSymbol methodSymbol ||
                !methodSymbol.HasAttributeWithType(symbols.PipelineAttribute))
            {
                continue;
            }

            if (!TryCollectPipeline(methodSymbol, symbols, out PipelineContractInfo? pipeline))
            {
                pipelines = default;

                return false;
            }

            builder.Add(pipeline);
        }

        if (builder.Count == 0)
        {
            pipelines = default;

            return false;
        }

        pipelines = builder.ToImmutable();

        return true;
    }

    /// <summary>
    /// Tries to collect a single annotated pipeline method.
    /// </summary>
    /// <param name="methodSymbol">The annotated pipeline method.</param>
    /// <param name="symbols">The well known symbols to resolve the declaration with.</param>
    /// <param name="pipeline">The resulting pipeline, if the method declares a valid contract.</param>
    /// <returns>Whether the pipeline could be collected.</returns>
    private static bool TryCollectPipeline(
        IMethodSymbol methodSymbol,
        PipelineWellKnownSymbols symbols,
        out PipelineContractInfo pipeline)
    {
        pipeline = null!;

        if (methodSymbol is not
            {
                DeclaredAccessibility: Accessibility.Private,
                IsStatic: false,
                IsGenericMethod: false,
                IsAsync: false,
                ReturnsVoid: true,
                Parameters: [{ RefKind: RefKind.In } contextParameter, ..]
            } ||
            !SymbolEqualityComparer.Default.Equals(contextParameter.Type, symbols.ComputeContext) ||
            !CanonicalSignatureBuilder.TryGetCanonicalSignature(methodSymbol, out string canonicalSignature))
        {
            return false;
        }

        using ImmutableArrayBuilder<ResourceContractInfo> parameterBuilder = new();

        for (int i = 1; i < methodSymbol.Parameters.Length; i++)
        {
            IParameterSymbol parameterSymbol = methodSymbol.Parameters[i];

            if (parameterSymbol.RefKind is not (RefKind.None or RefKind.In))
            {
                return false;
            }

            if (!parameterSymbol.TryGetAttributeWithType(symbols.ResourceAttribute, out AttributeData? attribute))
            {
                continue;
            }

            if (!TryGetResourceContract(attribute, out ComputeResourceAccess access, out ComputeResourceSharing sharing, out ComputeResourceAliasing aliasing) ||
                !parameterSymbol.Type.HasInterfaceWithType(symbols.GraphicsResourceInterface) ||
                !ComputeAccessContract.IsCompatible(parameterSymbol.Type, access))
            {
                return false;
            }

            parameterBuilder.Add(new ResourceContractInfo(
                uint.MaxValue,
                CanonicalTypeNameBuilder.GetCanonicalTypeName(parameterSymbol.Type),
                access,
                sharing,
                aliasing,
                ResourceOwnershipKind.Borrowed,
                false,
                0,
                0));
        }

        PipelineFlags flags = methodSymbol.HasAttributeWithType(symbols.InteropAttribute)
            ? PipelineFlags.InteropRoundTrip
            : PipelineFlags.None;

        pipeline = new PipelineContractInfo(
            uint.MaxValue,
            methodSymbol.MetadataName,
            canonicalSignature,
            flags,
            0,
            0,
            parameterBuilder.ToImmutable(),
            ImmutableArray<ResourceContractInfo>.Empty);

        return true;
    }

    /// <summary>
    /// Tries to get the declared contract values of a <c>[ComputeResource]</c> attribute.
    /// </summary>
    /// <param name="attribute">The attribute data to read.</param>
    /// <param name="access">The declared compute access.</param>
    /// <param name="sharing">The declared sharing mode.</param>
    /// <param name="aliasing">The declared aliasing mode.</param>
    /// <returns>Whether the attribute declares a supported contract.</returns>
    private static bool TryGetResourceContract(
        AttributeData attribute,
        out ComputeResourceAccess access,
        out ComputeResourceSharing sharing,
        out ComputeResourceAliasing aliasing)
    {
        access = default;
        sharing = ComputeResourceSharing.Internal;
        aliasing = ComputeResourceAliasing.Disallow;

        if (attribute.ConstructorArguments is not [{ Value: byte accessValue }])
        {
            return false;
        }

        access = (ComputeResourceAccess)accessValue;

        foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Value.Value is not byte namedValue)
            {
                return false;
            }

            switch (namedArgument.Key)
            {
                case "Sharing":
                    sharing = (ComputeResourceSharing)namedValue;
                    break;
                case "Aliasing":
                    aliasing = (ComputeResourceAliasing)namedValue;
                    break;
                default:
                    return false;
            }
        }

        return access is ComputeResourceAccess.Read or ComputeResourceAccess.Write or ComputeResourceAccess.ReadWrite &&
            sharing is ComputeResourceSharing.Internal or ComputeResourceSharing.External &&
            aliasing is ComputeResourceAliasing.Disallow or ComputeResourceAliasing.Allow;
    }
}
