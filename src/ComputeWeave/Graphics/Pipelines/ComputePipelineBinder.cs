using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;

namespace ComputeWeave;

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
    /// Pins the generation a given external binding refers to for the duration of the recording.
    /// </summary>
    /// <typeparam name="TResource">The type of the bound graphics resource.</typeparam>
    /// <param name="binding">The binding to pin the generation of.</param>
    /// <param name="resource">The resource the binding refers to, if its generation could be pinned.</param>
    /// <returns>Whether the generation <paramref name="binding"/> refers to could be pinned.</returns>
    /// <exception cref="GraphicsDeviceMismatchException">Thrown if the bound resource belongs to another device.</exception>
    /// <remarks>
    /// Resources shared with an external queue are pinned through this overload. Their slot is owned by a compute
    /// interop resource set rather than by the host, so the binding carries the slot it was produced from and the
    /// generation is revalidated under that slot gate rather than under one of the slots of the host.
    /// </remarks>
    public bool TryPin<TResource>(in ComputeResourceBinding<TResource> binding, out TResource resource)
        where TResource : class, IGraphicsResource
    {
        resource = null!;

        if (!binding.IsValid || binding.Slot is not IComputeGenerationPinSource slot)
        {
            return false;
        }

        if (binding.Resource!.GraphicsDevice != this.host.Device)
        {
            GraphicsDeviceMismatchException.Throw(binding.Resource, this.host.Device);
        }

        if (!slot.TryPinGeneration(
                binding.SetId,
                binding.GenerationId,
                binding.BindingEpoch,
                binding.ResourceIndex,
                out ResourceGenerationPin pin))
        {
            return false;
        }

        if (!ResourceGenerationPinTracker.TryAdd(this.host.Device, this.storage, ref this.bundle, in pin))
        {
            return false;
        }

        resource = binding.Resource;

        return true;
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
        return TryPin(slotOrdinal, in binding, out _);
    }

    /// <summary>
    /// Pins the generation a given binding refers to for the duration of the recording, and resolves the resource it refers to.
    /// </summary>
    /// <typeparam name="TResource">The type of the bound graphics resource.</typeparam>
    /// <param name="slotOrdinal">The ordinal of the owned slot the binding was produced from.</param>
    /// <param name="binding">The binding to pin the generation of.</param>
    /// <param name="resource">The resource the binding refers to, if its generation could be pinned.</param>
    /// <returns>Whether the generation <paramref name="binding"/> refers to could be pinned.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="slotOrdinal"/> is not a declared slot.</exception>
    /// <remarks>
    /// Pipeline methods declaring an owned resource parameter receive the resource resolved by this overload. Resolving
    /// it from the pin rather than from the slot is what makes the parameter refer to the pinned generation for the whole
    /// recording, as a concurrent resource plan replacement may publish another generation right after the pin is taken.
    /// </remarks>
    public bool TryPin<TResource>(int slotOrdinal, in ComputeResourceBinding<TResource> binding, out TResource resource)
        where TResource : class, IGraphicsResource
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(slotOrdinal);
        default(ArgumentOutOfRangeException).ThrowIfGreaterThanOrEqual(slotOrdinal, this.host.SlotCount);

        resource = null!;

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

        if (!ResourceGenerationPinTracker.TryAdd(this.host.Device, this.storage, ref this.bundle, in pin))
        {
            return false;
        }

        resource = binding.Resource!;

        return true;
    }
}
