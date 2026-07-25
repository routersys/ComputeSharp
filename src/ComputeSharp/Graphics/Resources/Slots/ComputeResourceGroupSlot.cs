using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a compute resource group declared by a compute pipeline host.
/// </summary>
/// <typeparam name="TGroup">The type of the owned compute resource group.</typeparam>
public sealed class ComputeResourceGroupSlot<TGroup> : IDisposable
    where TGroup : class
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
    /// Creates a new <see cref="ComputeResourceGroupSlot{TGroup}"/> instance that is not bound to a host.
    /// </summary>
    public ComputeResourceGroupSlot()
    {
    }

    /// <summary>
    /// Gets whether the current slot owns a published resource group generation.
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
                "The resource group slot is still bound to a pipeline host.");
        }
    }
}
