using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;
using ComputeWeave.Resources.Lifetime;

namespace ComputeWeave;

/// <summary>
/// A scoped operation that holds the external queue ownership of a compute interop domain.
/// </summary>
public readonly ref struct ExternalQueueOperation
{
    /// <summary>
    /// The domain the current operation holds the external queue ownership of.
    /// </summary>
    private readonly ComputeInteropDomain? domain;

    /// <summary>
    /// The token the current operation was acquired with.
    /// </summary>
    private readonly ulong token;

    /// <summary>
    /// The generation the current operation is bound to.
    /// </summary>
    private readonly ResourceGenerationId boundGeneration;

    /// <summary>
    /// Creates a new <see cref="ExternalQueueOperation"/> instance with the specified parameters.
    /// </summary>
    /// <param name="domain">The domain the operation holds the external queue ownership of.</param>
    /// <param name="token">The token the operation was acquired with.</param>
    /// <param name="boundGeneration">The generation the operation is bound to.</param>
    internal ExternalQueueOperation(ComputeInteropDomain domain, ulong token, ResourceGenerationId boundGeneration)
    {
        this.domain = domain;
        this.token = token;
        this.boundGeneration = boundGeneration;
    }

    /// <summary>
    /// Gets whether the current operation still holds the external queue ownership.
    /// </summary>
    /// <remarks>
    /// The domain owns the token the operation was acquired with, so a copy of a released scope reports an
    /// invalid operation rather than the ownership its token no longer stands for.
    /// </remarks>
    public bool IsValid => this.domain?.IsOperationActive(this.token) is true;

    /// <summary>
    /// Gets the domain the current operation holds the external queue ownership of.
    /// </summary>
    internal ComputeInteropDomain? Domain => this.domain;

    /// <summary>
    /// Gets whether the generation the current operation is bound to is available to the external queue.
    /// </summary>
    /// <param name="pin">The generation the caller holds an external reference of.</param>
    /// <returns>Whether the bound generation is available to the external queue.</returns>
    /// <remarks>
    /// The ownership of a generation belongs to the hazard gate of its device, so the check runs under that
    /// gate, before the caller enqueues any external queue work over the view of the generation.
    /// </remarks>
    internal bool IsBoundGenerationAvailable(in ResourceGenerationPin pin)
    {
        if (this.domain is not ComputeInteropDomain boundDomain)
        {
            return false;
        }

        lock (boundDomain.Device.HazardGate)
        {
            ref ResourceGenerationRecord record = ref pin.Handle.Owner.GetResourceRecord(pin.ResourceIndex);

            return record.Id == this.boundGeneration && record.ReadOwnership() is ExternalOwnershipState.ExternalAvailable;
        }
    }

    /// <summary>
    /// Releases the external queue ownership and the borrowed external reference the current operation holds.
    /// </summary>
    /// <param name="pin">The generation the borrow took an external reference of.</param>
    internal void ReleaseBorrow(in ResourceGenerationPin pin)
    {
        this.domain?.ReleaseOperation(ExternalDomainReference.TransientOperation, this.token, in pin);
    }

    /// <summary>
    /// Releases the external queue ownership held by the current operation.
    /// </summary>
    public void Dispose()
    {
        this.domain?.ReleaseOperation(ExternalDomainReference.TransientOperation, this.token);
    }
}
