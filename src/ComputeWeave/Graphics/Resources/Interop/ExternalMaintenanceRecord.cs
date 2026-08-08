using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;

namespace ComputeWeave.Resources.Interop;

internal struct ExternalMaintenanceRecord(
    ExternalDomainId domain,
    ResourceSetRegistrationId resourceSet,
    SlotOrdinal slot)
{
    public ExternalDrainState State;

    public int Queued;

    /// <summary>
    /// Whether a maintenance pass holds the right to run a phase of the current record.
    /// </summary>
    /// <remarks>
    /// The phase a pass has to run is decided under the maintenance exclusion of the slot, but the phase itself
    /// runs outside it, and the record only advances once the phase has already signalled the external queue.
    /// Two passes that decide at the same time would therefore both issue the final drain of one generation.
    /// Section 15.4 of the pipeline interop specification forbids that duplicate, so a pass claims the record
    /// before it leaves the exclusion. The exclusion serialises every access, so no interlocked access is used.
    /// </remarks>
    public int Executing;

    public ExternalDomainId Domain = domain;

    public ResourceSetRegistrationId ResourceSet = resourceSet;

    public SlotOrdinal Slot = slot;

    public ResourceGenerationId Generation;

    public FencePoint RetirementFence;

    public readonly bool IsIdle => this.State is ExternalDrainState.None;

    public readonly bool IsCompleted => this.State is ExternalDrainState.Completed;

    public readonly bool IsFaulted => this.State is ExternalDrainState.Faulted;

    /// <summary>
    /// Claims the right to run a phase of the current record.
    /// </summary>
    /// <returns>Whether the claim was taken.</returns>
    public bool TryBeginPhase()
    {
        if (this.Executing != 0)
        {
            return false;
        }

        this.Executing = 1;

        return true;
    }

    /// <summary>
    /// Releases the claim a pass took with <see cref="TryBeginPhase"/>.
    /// </summary>
    /// <returns>Whether a claim was held.</returns>
    public bool TryEndPhase()
    {
        if (this.Executing == 0)
        {
            return false;
        }

        this.Executing = 0;

        return true;
    }

    public bool TryRequest(ResourceGenerationId generation)
    {
        if (this.State is not ExternalDrainState.None)
        {
            return false;
        }

        this.State = ExternalDrainState.Requested;
        this.Generation = generation;
        this.RetirementFence = FencePoint.None;

        return true;
    }

    public bool TryQueue()
    {
        if (this.State is not ExternalDrainState.Requested)
        {
            return false;
        }

        this.State = ExternalDrainState.Queued;
        this.Queued = 1;

        return true;
    }

    public bool TryWaitForDomainPermit()
    {
        if (this.State is not ExternalDrainState.Queued)
        {
            return false;
        }

        this.State = ExternalDrainState.WaitingForDomainPermit;

        return true;
    }

    public bool TryWaitForScheduler()
    {
        if (this.State is not (ExternalDrainState.Queued
            or ExternalDrainState.WaitingForDomainPermit
            or ExternalDrainState.ExternalReleasePending))
        {
            return false;
        }

        this.State = ExternalDrainState.WaitingForScheduler;

        return true;
    }

    public bool TrySkipFinalDrain()
    {
        if (this.State is not ExternalDrainState.Queued)
        {
            return false;
        }

        this.State = ExternalDrainState.ExternalReleasePending;
        this.RetirementFence = FencePoint.None;

        return true;
    }

    public bool TryIssueFinalDrain(FencePoint retirementFence)
    {
        if (this.State is not (ExternalDrainState.Queued
            or ExternalDrainState.WaitingForDomainPermit
            or ExternalDrainState.WaitingForScheduler))
        {
            return false;
        }

        this.State = ExternalDrainState.FenceIssued;
        this.RetirementFence = retirementFence;

        return true;
    }

    public bool TryWaitForFence()
    {
        if (this.State is not ExternalDrainState.FenceIssued)
        {
            return false;
        }

        this.State = ExternalDrainState.WaitingFence;

        return true;
    }

    public bool TryCompleteFinalDrain()
    {
        if (this.State is not (ExternalDrainState.FenceIssued or ExternalDrainState.WaitingFence))
        {
            return false;
        }

        this.State = ExternalDrainState.ExternalReleasePending;

        return true;
    }

    public bool TryComplete()
    {
        if (this.State is not (ExternalDrainState.ExternalReleasePending
            or ExternalDrainState.WaitingForScheduler
            or ExternalDrainState.Faulted))
        {
            return false;
        }

        this.State = ExternalDrainState.Completed;
        this.Queued = 0;

        return true;
    }

    public bool TryFault()
    {
        if (this.State is ExternalDrainState.None or ExternalDrainState.Completed or ExternalDrainState.Faulted)
        {
            return false;
        }

        this.State = ExternalDrainState.Faulted;

        return true;
    }

    public bool TryReset()
    {
        if (this.State is not ExternalDrainState.Completed)
        {
            return false;
        }

        this.State = ExternalDrainState.None;
        this.Generation = default;
        this.RetirementFence = FencePoint.None;

        return true;
    }
}
