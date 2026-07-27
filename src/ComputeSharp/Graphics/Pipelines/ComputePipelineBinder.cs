using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// The binder a generated pipeline invocation pins its bound resources into, in contract ordinal order.
/// </summary>
public readonly ref struct ComputePipelineBinder
{
    /// <summary>
    /// The device the pinned resources must belong to.
    /// </summary>
    private readonly GraphicsDevice device;

    /// <summary>
    /// The pin storage of the recording bundle in use.
    /// </summary>
    private readonly Span<ResourceGenerationPin> storage;

    /// <summary>
    /// The recording bundle the pins are tracked into.
    /// </summary>
    private readonly ref RecordingBundleEntry bundle;

    /// <summary>
    /// Creates a new <see cref="ComputePipelineBinder"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The device the pinned resources must belong to.</param>
    /// <param name="storage">The pin storage of the recording bundle in use.</param>
    /// <param name="bundle">The recording bundle the pins are tracked into.</param>
    internal ComputePipelineBinder(GraphicsDevice device, Span<ResourceGenerationPin> storage, ref RecordingBundleEntry bundle)
    {
        this.device = device;
        this.storage = storage;
        this.bundle = ref bundle;
    }

    /// <summary>
    /// Pins the generation currently bound to a given resource for the duration of the recording.
    /// </summary>
    /// <param name="resource">The resource to pin the generation of.</param>
    /// <returns>Whether the generation of <paramref name="resource"/> could be pinned.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resource"/> is <see langword="null"/>.</exception>
    /// <exception cref="GraphicsDeviceMismatchException">Thrown if <paramref name="resource"/> belongs to another device.</exception>
    /// <remarks>
    /// Resources must be pinned in contract ordinal order, that is every pipeline parameter in declaration
    /// order followed by every internal resource in declaration order. The runtime matches the pinned
    /// generations against the declared contracts by that order.
    /// </remarks>
    public bool TryPin(IGraphicsResource resource)
    {
        return ResourceGenerationPinTracker.TryPin(this.device, this.storage, ref this.bundle, resource);
    }
}
