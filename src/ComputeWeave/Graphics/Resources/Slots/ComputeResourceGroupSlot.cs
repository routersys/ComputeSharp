using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;

namespace ComputeWeave;

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
    /// The registry of the device the current slot is bound through, or <see langword="null"/> if it is not bound.
    /// </summary>
    private DeviceRegistrationRegistry? registry;

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
    bool IComputeOwnedSlot.TryBind(DeviceRegistrationRegistry registry, int[] planStorage, in SlotResourcePlanStateRecord planState)
    {
        this.registry = registry;

        if (this.slotGate.TryBind(planStorage, in planState))
        {
            return true;
        }

        this.registry = null;

        return false;
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
    void IComputeOwnedSlot.MarkTerminalRetained()
    {
        _ = this.slotGate.TryMarkDeviceTerminal();
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.ReleaseTerminalGenerations()
    {
        SlotTerminalRelease.Run(ref this.slotGate);
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.RunMaintenance()
    {
        SlotGenerationMaintenance.Run(ref this.slotGate);
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryTrim()
    {
        return this.slotGate.TryTrim();
    }

    /// <inheritdoc/>
    bool IComputeOwnedSlot.TryGetTrimCandidate(out SlotTrimCandidate candidate)
    {
        return this.slotGate.TryGetTrimCandidate(out candidate);
    }

    /// <inheritdoc/>
    void IComputeOwnedSlot.GetGenerationCounts(ref int activeCount, ref int retiredCount)
    {
        this.slotGate.GetGenerationCounts(ref activeCount, ref retiredCount);
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
        return this.slotGate.TryGetBinding(this, resourceIndex, out binding);
    }

    /// <inheritdoc/>
    bool IComputeGenerationPinSource.TryPinGeneration(
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex,
        out ResourceGenerationPin pin)
    {
        return this.slotGate.TryPin(setId, generationId, bindingEpoch, resourceIndex, out pin);
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
    /// <exception cref="InvalidOperationException">Thrown if disposal of the current slot has not been requested.</exception>
    /// <remarks>
    /// A slot that is not bound to a host has nothing to wait for and returns immediately.
    /// </remarks>
    public void WaitForDisposal()
    {
        if (this.registry is not DeviceRegistrationRegistry registry)
        {
            return;
        }

        SlotDisposalWait.Run(ref this.slotGate, registry, "The resource group slot has not been disposed.");
    }
}
