using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp;

/// <summary>
/// A binding to a specific generation of a graphics resource, as produced by a generated pipeline host.
/// </summary>
/// <typeparam name="TResource">The type of the bound graphics resource.</typeparam>
public readonly struct ComputeResourceBinding<TResource>
    where TResource : class, IGraphicsResource
{
    /// <summary>
    /// The bound resource, or <see langword="null"/> for an invalid binding.
    /// </summary>
    internal readonly TResource? Resource;

    /// <summary>
    /// The id of the generation set the binding was produced from.
    /// </summary>
    internal readonly ResourceGenerationSetId SetId;

    /// <summary>
    /// The id of the generation the binding was produced from.
    /// </summary>
    internal readonly ResourceGenerationId GenerationId;

    /// <summary>
    /// The binding epoch of the slot the binding was produced from.
    /// </summary>
    internal readonly ulong BindingEpoch;

    /// <summary>
    /// The index of the resource within its generation set.
    /// </summary>
    internal readonly int ResourceIndex;

    /// <summary>
    /// Creates a new <see cref="ComputeResourceBinding{TResource}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="resource">The bound resource.</param>
    /// <param name="setId">The id of the generation set the binding was produced from.</param>
    /// <param name="generationId">The id of the generation the binding was produced from.</param>
    /// <param name="bindingEpoch">The binding epoch of the slot the binding was produced from.</param>
    /// <param name="resourceIndex">The index of the resource within its generation set.</param>
    internal ComputeResourceBinding(
        TResource resource,
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex)
    {
        this.Resource = resource;
        this.SetId = setId;
        this.GenerationId = generationId;
        this.BindingEpoch = bindingEpoch;
        this.ResourceIndex = resourceIndex;
    }

    /// <summary>
    /// Gets whether the current binding refers to a published generation.
    /// </summary>
    internal bool IsValid => this.Resource is not null;
}
