using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a single graphics resource declared by a compute pipeline host.
/// </summary>
/// <typeparam name="TResource">The type of the owned graphics resource.</typeparam>
public sealed class ComputeResourceSlot<TResource> : IDisposable
    where TResource : class, IGraphicsResource
{
    /// <summary>
    /// The gate protecting the state of the current slot.
    /// </summary>
    private SlotGate slotGate;

    /// <summary>
    /// Creates a new <see cref="ComputeResourceSlot{TResource}"/> instance that is not bound to a host.
    /// </summary>
    public ComputeResourceSlot()
    {
    }

    /// <summary>
    /// Gets whether the current slot owns a published resource generation.
    /// </summary>
    public bool IsAllocated => this.slotGate.IsAllocated;

    /// <summary>
    /// Gets whether disposal of the current slot has been requested.
    /// </summary>
    public bool IsDisposeRequested => this.slotGate.IsDisposeRequested;

    /// <inheritdoc/>
    public void Dispose()
    {
        PreparedGenerationRollback.RollbackUnpublished(this.slotGate.RequestDispose());
    }

    /// <summary>
    /// Waits for the disposal of the current slot to complete.
    /// </summary>
    public void WaitForDisposal()
    {
        default(InvalidOperationException).ThrowIf(
            !this.slotGate.IsDisposalComplete,
            "The resource slot is still bound to a pipeline host.");
    }
}
