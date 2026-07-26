using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a compute resource group declared by a compute pipeline host.
/// </summary>
/// <typeparam name="TGroup">The type of the owned compute resource group.</typeparam>
public sealed class ComputeResourceGroupSlot<TGroup> : IComputeOwnedResourceSlot, IDisposable, IComputeOwnedSlot
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
    bool IComputeOwnedSlot.IsDisposalComplete => this.slotGate.IsDisposalComplete;

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryBind(int[] planStorage, in SlotResourcePlanStateRecord planState)
    {
        return this.slotGate.TryBind(planStorage, in planState);
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.RequestDispose()
    {
        Dispose();
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.ThrowIfUnbound()
    {
        this.slotGate.ThrowIfUnbound();
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.RunMaintenance()
    {
        SlotGenerationMaintenance.Run(ref this.slotGate);
    }

    /// <inheritdoc/>
    ResourcePlanDecision IComputeOwnedSlot.Evaluate(in OwnedSlotDescriptor descriptor, ReadOnlySpan<int> requestedPlan)
    {
        return this.slotGate.Evaluate(in descriptor, requestedPlan);
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch)
    {
        this.slotGate.GetActiveSnapshot(out activeSetId, out bindingEpoch);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryApplyLogicalUpdate(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ReadOnlySpan<int> requestedPlan)
    {
        return this.slotGate.TryApplyLogicalUpdate(expectedActiveSetId, expectedBindingEpoch, requestedPlan);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken, ReadOnlySpan<int> requestedPlan)
    {
        return this.slotGate.TryInstallPrepared(prepared, preparedToken, requestedPlan);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryCommitReplacement(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        return this.slotGate.TryCommitReplacement(expectedActiveSetId, expectedBindingEpoch, preparedToken, out detachedPrepared);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryAbortReplacement(ulong preparedToken, out ResourceGenerationSetHandle detachedPrepared)
    {
        return this.slotGate.TryAbortReplacement(preparedToken, out detachedPrepared);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryGetBinding<TResource>(int resourceIndex, out ComputeResourceBinding<TResource> binding)
        where TResource : class
    {
        return this.slotGate.TryGetBinding(resourceIndex, out binding);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        PreparedGenerationRollback.RollbackUnpublished(this.slotGate.RequestDispose());

        SlotGenerationMaintenance.Run(ref this.slotGate);
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
