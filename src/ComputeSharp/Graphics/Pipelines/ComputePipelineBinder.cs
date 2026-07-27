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
    /// The host runtime owning the slots and the recording bundle in use.
    /// </summary>
    private readonly PipelineHostRuntime host;

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
    /// <param name="host">The host runtime owning the slots and the recording bundle in use.</param>
    /// <param name="storage">The pin storage of the recording bundle in use.</param>
    /// <param name="bundle">The recording bundle the pins are tracked into.</param>
    internal ComputePipelineBinder(PipelineHostRuntime host, Span<ResourceGenerationPin> storage, ref RecordingBundleEntry bundle)
    {
        this.host = host;
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
        return ResourceGenerationPinTracker.TryPin(this.host.Device, this.storage, ref this.bundle, resource);
    }

    /// <summary>
    /// Pins the generation a given binding refers to for the duration of the recording.
    /// </summary>
    /// <typeparam name="TResource">The type of the bound graphics resource.</typeparam>
    /// <param name="slotOrdinal">The ordinal of the owned slot the binding was produced from.</param>
    /// <param name="binding">The binding to pin the generation of.</param>
    /// <returns>Whether the generation <paramref name="binding"/> refers to could be pinned.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="slotOrdinal"/> is not a declared slot.</exception>
    /// <remarks>
    /// Resources owned by a slot must be pinned through this overload, as it revalidates the slot control
    /// state, the active generation pointer and the binding epoch under the slot gate. Passing such a
    /// resource to the overload taking a resource would skip those checks.
    /// </remarks>
    public bool TryPin<TResource>(int slotOrdinal, in ComputeResourceBinding<TResource> binding)
        where TResource : class, IGraphicsResource
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(slotOrdinal);
        default(ArgumentOutOfRangeException).ThrowIfGreaterThanOrEqual(slotOrdinal, this.host.SlotCount);

        if (!binding.IsValid ||
            !this.host.GetSlot(slotOrdinal).TryPinGeneration(
                binding.SetId,
                binding.GenerationId,
                binding.BindingEpoch,
                binding.ResourceIndex,
                out ResourceGenerationPin pin))
        {
            return false;
        }

        return ResourceGenerationPinTracker.TryAdd(this.host.Device, this.storage, ref this.bundle, in pin);
    }
}
