using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The well known symbols needed to build a pipeline contract model.
/// </summary>
/// <param name="pipelineHostAttribute">The <c>[ComputePipelineHost]</c> symbol.</param>
/// <param name="pipelineAttribute">The <c>[ComputePipeline]</c> symbol.</param>
/// <param name="interopAttribute">The <c>[ComputeInterop]</c> symbol.</param>
/// <param name="resourceAttribute">The <c>[ComputeResource]</c> symbol.</param>
/// <param name="pipelineResourceAttribute">The <c>[ComputePipelineResource]</c> symbol.</param>
/// <param name="resourceGroupAttribute">The <c>[ComputeResourceGroup]</c> symbol.</param>
/// <param name="interopResourceSetAttribute">The <c>[ComputeInteropResourceSet]</c> symbol.</param>
/// <param name="sharedTextureAttribute">The <c>[ComputeSharedTexture]</c> symbol.</param>
/// <param name="graphicsResourceInterface">The <see cref="IGraphicsResource"/> symbol.</param>
/// <param name="computeContext">The <c>ComputeContext</c> symbol.</param>
/// <param name="resourceSlot">The <c>ComputeResourceSlot&lt;TResource&gt;</c> symbol.</param>
/// <param name="resourceGroupSlot">The <c>ComputeResourceGroupSlot&lt;TGroup&gt;</c> symbol.</param>
/// <param name="sharedTextureSlot">The <c>SharedTextureSlot&lt;T, TPixel, TView&gt;</c> symbol.</param>
internal sealed class PipelineWellKnownSymbols(
    INamedTypeSymbol pipelineHostAttribute,
    INamedTypeSymbol pipelineAttribute,
    INamedTypeSymbol interopAttribute,
    INamedTypeSymbol resourceAttribute,
    INamedTypeSymbol pipelineResourceAttribute,
    INamedTypeSymbol resourceGroupAttribute,
    INamedTypeSymbol interopResourceSetAttribute,
    INamedTypeSymbol sharedTextureAttribute,
    INamedTypeSymbol graphicsResourceInterface,
    INamedTypeSymbol computeContext,
    INamedTypeSymbol resourceSlot,
    INamedTypeSymbol resourceGroupSlot,
    INamedTypeSymbol sharedTextureSlot)
{
    /// <summary>
    /// Gets the <c>[ComputePipelineHost]</c> symbol.
    /// </summary>
    public INamedTypeSymbol PipelineHostAttribute { get; } = pipelineHostAttribute;

    /// <summary>
    /// Gets the <c>[ComputePipeline]</c> symbol.
    /// </summary>
    public INamedTypeSymbol PipelineAttribute { get; } = pipelineAttribute;

    /// <summary>
    /// Gets the <c>[ComputeInterop]</c> symbol.
    /// </summary>
    public INamedTypeSymbol InteropAttribute { get; } = interopAttribute;

    /// <summary>
    /// Gets the <c>[ComputeResource]</c> symbol.
    /// </summary>
    public INamedTypeSymbol ResourceAttribute { get; } = resourceAttribute;

    /// <summary>
    /// Gets the <c>[ComputePipelineResource]</c> symbol.
    /// </summary>
    public INamedTypeSymbol PipelineResourceAttribute { get; } = pipelineResourceAttribute;

    /// <summary>
    /// Gets the <c>[ComputeResourceGroup]</c> symbol.
    /// </summary>
    public INamedTypeSymbol ResourceGroupAttribute { get; } = resourceGroupAttribute;

    /// <summary>
    /// Gets the <c>[ComputeInteropResourceSet]</c> symbol.
    /// </summary>
    public INamedTypeSymbol InteropResourceSetAttribute { get; } = interopResourceSetAttribute;

    /// <summary>
    /// Gets the <c>[ComputeSharedTexture]</c> symbol.
    /// </summary>
    public INamedTypeSymbol SharedTextureAttribute { get; } = sharedTextureAttribute;

    /// <summary>
    /// Gets the <see cref="IGraphicsResource"/> symbol.
    /// </summary>
    public INamedTypeSymbol GraphicsResourceInterface { get; } = graphicsResourceInterface;

    /// <summary>
    /// Gets the <c>ComputeContext</c> symbol.
    /// </summary>
    public INamedTypeSymbol ComputeContext { get; } = computeContext;

    /// <summary>
    /// Gets the <c>ComputeResourceSlot&lt;TResource&gt;</c> symbol.
    /// </summary>
    public INamedTypeSymbol ResourceSlot { get; } = resourceSlot;

    /// <summary>
    /// Gets the <c>ComputeResourceGroupSlot&lt;TGroup&gt;</c> symbol.
    /// </summary>
    public INamedTypeSymbol ResourceGroupSlot { get; } = resourceGroupSlot;

    /// <summary>
    /// Gets the <c>SharedTextureSlot&lt;T, TPixel, TView&gt;</c> symbol.
    /// </summary>
    public INamedTypeSymbol SharedTextureSlot { get; } = sharedTextureSlot;

    /// <summary>
    /// Tries to resolve all well known symbols from a given compilation.
    /// </summary>
    /// <param name="compilation">The compilation to resolve the symbols from.</param>
    /// <param name="symbols">The resulting symbols, if all of them could be resolved.</param>
    /// <returns>Whether all well known symbols could be resolved.</returns>
    public static bool TryCreate(Compilation compilation, [NotNullWhen(true)] out PipelineWellKnownSymbols? symbols)
    {
        if (compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineHostAttribute") is not { } pipelineHostAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeInteropAttribute") is not { } interopAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceAttribute") is not { } resourceAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineResourceAttribute") is not { } pipelineResourceAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupAttribute") is not { } resourceGroupAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeInteropResourceSetAttribute") is not { } interopResourceSetAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeSharedTextureAttribute") is not { } sharedTextureAttribute ||
            compilation.GetTypeByMetadataName("ComputeSharp.IGraphicsResource") is not { } graphicsResourceInterface ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeContext") is not { } computeContext ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceSlot`1") is not { } resourceSlot ||
            compilation.GetTypeByMetadataName("ComputeSharp.ComputeResourceGroupSlot`1") is not { } resourceGroupSlot ||
            compilation.GetTypeByMetadataName("ComputeSharp.SharedTextureSlot`3") is not { } sharedTextureSlot)
        {
            symbols = null;

            return false;
        }

        symbols = new PipelineWellKnownSymbols(
            pipelineHostAttribute,
            pipelineAttribute,
            interopAttribute,
            resourceAttribute,
            pipelineResourceAttribute,
            resourceGroupAttribute,
            interopResourceSetAttribute,
            sharedTextureAttribute,
            graphicsResourceInterface,
            computeContext,
            resourceSlot,
            resourceGroupSlot,
            sharedTextureSlot);

        return true;
    }
}
