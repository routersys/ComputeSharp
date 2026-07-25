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
    /// The lock protecting <see cref="control"/>.
    /// </summary>
    private readonly object slotGate = new();

    /// <summary>
    /// The control record for the current slot.
    /// </summary>
    private SlotControlRecord control;

    /// <summary>
    /// Creates a new <see cref="ComputeResourceSlot{TResource}"/> instance that is not bound to a host.
    /// </summary>
    public ComputeResourceSlot()
    {
    }

    /// <summary>
    /// Gets whether the current slot owns a published resource generation.
    /// </summary>
    public bool IsAllocated
    {
        get
        {
            lock (this.slotGate)
            {
                return this.control.IsAllocated;
            }
        }
    }

    /// <summary>
    /// Gets whether disposal of the current slot has been requested.
    /// </summary>
    public bool IsDisposeRequested
    {
        get
        {
            lock (this.slotGate)
            {
                return this.control.IsDisposeRequested;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.slotGate)
        {
            _ = this.control.RequestDispose();
        }
    }

    /// <summary>
    /// Waits for the disposal of the current slot to complete.
    /// </summary>
    public void WaitForDisposal()
    {
        lock (this.slotGate)
        {
            default(InvalidOperationException).ThrowIf(
                this.control.State is not (SlotControlState.Unbound or SlotControlState.Disposed),
                "The resource slot is still bound to a pipeline host.");
        }
    }
}
