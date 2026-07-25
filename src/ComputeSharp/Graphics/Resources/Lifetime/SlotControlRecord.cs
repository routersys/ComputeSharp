using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal struct SlotControlRecord
{
    public SlotControlState State;

    public ulong BindingEpoch;

    public ulong PreparedToken;

    public ResourceGenerationSetHandle Active;

    public ResourceGenerationSetHandle Prepared;

    public ResourceGenerationSetHandle Retired;

    public bool IsDisposeRequested;

    public readonly bool IsAllocated => this.State is SlotControlState.Active or SlotControlState.ReplacementPrepared && !this.Active.IsEmpty;

    public bool TryBind()
    {
        if (this.State is not SlotControlState.Unbound)
        {
            return false;
        }

        this.State = SlotControlState.Active;

        return true;
    }

    public readonly bool TryPin(ResourceGenerationSetId setId, ResourceGenerationId generationId, ulong bindingEpoch, int resourceIndex)
    {
        if (this.IsDisposeRequested ||
            this.State is not (SlotControlState.Active or SlotControlState.ReplacementPrepared) ||
            this.Active.IsEmpty ||
            this.Active.SetId != setId ||
            this.BindingEpoch != bindingEpoch)
        {
            return false;
        }

        ref ResourceGenerationRecord record = ref this.Active.Owner.GetResourceRecord(resourceIndex);

        if (record.Id != generationId)
        {
            return false;
        }

        return record.TryAcquireRecordingReference();
    }

    public readonly void ReleasePin(int resourceIndex)
    {
        this.Active.Owner.GetResourceRecord(resourceIndex).ReleaseRecordingReference();
    }

    public bool TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken)
    {
        if (this.IsDisposeRequested ||
            this.State is not SlotControlState.Active ||
            !this.Prepared.IsEmpty ||
            !this.Retired.IsEmpty ||
            preparedToken == 0)
        {
            return false;
        }

        this.Prepared = prepared;
        this.PreparedToken = preparedToken;
        this.State = SlotControlState.ReplacementPrepared;

        return true;
    }

    public bool TryCommitReplacement(ResourceGenerationSetId expectedActiveSetId, ulong expectedBindingEpoch, ulong preparedToken)
    {
        if (this.State is not SlotControlState.ReplacementPrepared ||
            this.IsDisposeRequested ||
            this.PreparedToken != preparedToken ||
            !this.Retired.IsEmpty ||
            this.BindingEpoch != expectedBindingEpoch ||
            ActiveSetId() != expectedActiveSetId)
        {
            _ = TryAbortReplacement(preparedToken);

            return false;
        }

        if (!this.Active.IsEmpty)
        {
            this.Retired = this.Active;
        }

        this.Active = this.Prepared;
        this.Prepared = default;
        this.PreparedToken = 0;
        this.State = SlotControlState.Active;
        this.BindingEpoch = checked(this.BindingEpoch + 1);

        return true;
    }

    public bool TryAbortReplacement(ulong preparedToken)
    {
        if (this.State is not SlotControlState.ReplacementPrepared || this.PreparedToken != preparedToken)
        {
            return false;
        }

        this.Prepared = default;
        this.PreparedToken = 0;
        this.State = SlotControlState.Active;

        return true;
    }

    public bool TryTrim()
    {
        if (this.State is not SlotControlState.Active ||
            this.IsDisposeRequested ||
            !this.Prepared.IsEmpty ||
            !this.Retired.IsEmpty ||
            this.Active.IsEmpty)
        {
            return false;
        }

        this.Retired = this.Active;
        this.Active = default;
        this.BindingEpoch = checked(this.BindingEpoch + 1);

        return true;
    }

    public void RequestDispose()
    {
        this.IsDisposeRequested = true;

        if (this.State is SlotControlState.Disposed)
        {
            return;
        }

        if (this.State is SlotControlState.Unbound)
        {
            this.State = SlotControlState.Disposed;

            return;
        }

        if (this.State is SlotControlState.ReplacementPrepared)
        {
            this.Prepared = default;
            this.PreparedToken = 0;
        }

        if (!this.Retired.IsEmpty)
        {
            this.State = SlotControlState.DisposeWaitingForRetired;

            return;
        }

        if (this.Active.IsEmpty)
        {
            this.State = SlotControlState.Disposed;

            return;
        }

        this.State = SlotControlState.RetiringActive;
    }

    public bool TryClearRetired()
    {
        if (this.Retired.IsEmpty)
        {
            return false;
        }

        this.Retired = default;

        if (this.State is SlotControlState.DisposeWaitingForRetired)
        {
            if (this.Active.IsEmpty)
            {
                this.State = SlotControlState.Disposed;
            }
            else
            {
                this.State = SlotControlState.RetiringActive;
            }
        }

        return true;
    }

    public bool TryCompleteRetiringActive()
    {
        if (this.State is not SlotControlState.RetiringActive)
        {
            return false;
        }

        this.Active = default;
        this.State = SlotControlState.Disposed;

        return true;
    }

    public bool TryMarkDeviceTerminal()
    {
        if (this.State is SlotControlState.Disposed)
        {
            return false;
        }

        this.Prepared = default;
        this.PreparedToken = 0;
        this.State = SlotControlState.RetiringActive;

        return true;
    }

    private readonly ResourceGenerationSetId ActiveSetId()
    {
        return this.Active.IsEmpty ? default : this.Active.SetId;
    }
}
