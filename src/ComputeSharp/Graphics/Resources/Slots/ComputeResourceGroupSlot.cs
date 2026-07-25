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
    /// The gate protecting the state of the current slot.
    /// </summary>
    private SlotGate slotGate;

    /// <summary>
    /// Creates a new <see cref="ComputeResourceGroupSlot{TGroup}"/> instance that is not bound to a host.
    /// </summary>
    public ComputeResourceGroupSlot()
    {
    }

    /// <summary>
    /// Gets whether the current slot owns a published resource group generation.
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
            "The resource group slot is still bound to a pipeline host.");
    }
}
