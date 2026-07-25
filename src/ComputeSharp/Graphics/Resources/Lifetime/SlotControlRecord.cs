using System;
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

    public readonly bool TryPin(
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex,
        out ResourceGenerationPin pin)
    {
        pin = default;

        ResourceGenerationSetHandle active = this.Active;

        if (this.IsDisposeRequested ||
            this.State is not (SlotControlState.Active or SlotControlState.ReplacementPrepared) ||
            active.IsEmpty ||
            active.SetId != setId ||
            this.BindingEpoch != bindingEpoch ||
            (uint)resourceIndex >= (uint)active.Owner.ResourceCount)
        {
            return false;
        }

        ref ResourceGenerationRecord record = ref active.Owner.GetResourceRecord(resourceIndex);

        if (record.Id != generationId || !record.TryAcquireRecordingReference())
        {
            return false;
        }

        pin = new ResourceGenerationPin(active, generationId, resourceIndex);

        return true;
    }

    public static void ReleasePin(in ResourceGenerationPin pin)
    {
        ref ResourceGenerationRecord record = ref pin.Handle.Owner.GetResourceRecord(pin.ResourceIndex);

        default(InvalidOperationException).ThrowIf(record.Id != pin.GenerationId, "The pinned generation no longer matches.");

        record.ReleaseRecordingReference();
    }

    public readonly bool CanApplyLogicalUpdate(ResourceGenerationSetId expectedActiveSetId, ulong expectedBindingEpoch)
    {
        return !this.IsDisposeRequested &&
            this.State is SlotControlState.Active &&
            !this.Active.IsEmpty &&
            this.Active.SetId == expectedActiveSetId &&
            this.BindingEpoch == expectedBindingEpoch;
    }

    public bool TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken)
    {
        if (this.IsDisposeRequested ||
            this.State is not SlotControlState.Active ||
            prepared.IsEmpty ||
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

    public bool TryCommitReplacement(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        detachedPrepared = default;

        if (this.State is not SlotControlState.ReplacementPrepared ||
            this.IsDisposeRequested ||
            this.PreparedToken != preparedToken ||
            !this.Retired.IsEmpty ||
            this.BindingEpoch != expectedBindingEpoch ||
            ActiveSetId() != expectedActiveSetId)
        {
            _ = TryAbortReplacement(preparedToken, out detachedPrepared);

            return false;
        }

        if (!this.Active.IsEmpty)
        {
            RetireAndReleaseOwnership(this.Active);

            this.Retired = this.Active;
        }

        this.Active = this.Prepared;
        this.Prepared = default;
        this.PreparedToken = 0;
        this.State = SlotControlState.Active;
        this.BindingEpoch = checked(this.BindingEpoch + 1);

        return true;
    }

    public bool TryAbortReplacement(ulong preparedToken, out ResourceGenerationSetHandle detachedPrepared)
    {
        detachedPrepared = default;

        if (this.State is not SlotControlState.ReplacementPrepared || this.PreparedToken != preparedToken)
        {
            return false;
        }

        detachedPrepared = this.Prepared;

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

        RetireAndReleaseOwnership(this.Active);

        this.Retired = this.Active;
        this.Active = default;
        this.BindingEpoch = checked(this.BindingEpoch + 1);

        return true;
    }

    public ResourceGenerationSetHandle RequestDispose()
    {
        this.IsDisposeRequested = true;

        ResourceGenerationSetHandle detachedPrepared = default;

        if (this.State is SlotControlState.Disposed or SlotControlState.DisposeWaitingForRetired or SlotControlState.RetiringActive)
        {
            return detachedPrepared;
        }

        if (this.State is SlotControlState.Unbound)
        {
            this.State = SlotControlState.Disposed;

            return detachedPrepared;
        }

        if (this.State is SlotControlState.ReplacementPrepared)
        {
            detachedPrepared = this.Prepared;

            this.Prepared = default;
            this.PreparedToken = 0;
        }

        if (!this.Retired.IsEmpty)
        {
            this.State = SlotControlState.DisposeWaitingForRetired;

            return detachedPrepared;
        }

        if (this.Active.IsEmpty)
        {
            this.State = SlotControlState.Disposed;

            return detachedPrepared;
        }

        RetireAndReleaseOwnership(this.Active);

        this.State = SlotControlState.RetiringActive;

        return detachedPrepared;
    }

    public bool TryClearRetired(ResourceGenerationSetId expectedSetId)
    {
        if (this.Retired.IsEmpty || this.Retired.SetId != expectedSetId || !AreAllReleased(this.Retired))
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
                RetireAndReleaseOwnership(this.Active);

                this.State = SlotControlState.RetiringActive;
            }
        }

        return true;
    }

    public bool TryCompleteRetiringActive()
    {
        if (this.State is not SlotControlState.RetiringActive ||
            !AreAllReleased(this.Active) ||
            !AreAllReleased(this.Prepared) ||
            !AreAllReleased(this.Retired))
        {
            return false;
        }

        bool activeChanged = !this.Active.IsEmpty;

        this.Active = default;
        this.Prepared = default;
        this.Retired = default;
        this.PreparedToken = 0;
        this.State = SlotControlState.Disposed;

        if (activeChanged)
        {
            this.BindingEpoch = checked(this.BindingEpoch + 1);
        }

        return true;
    }

    public bool TryMarkDeviceTerminal()
    {
        if (this.State is SlotControlState.Disposed)
        {
            return false;
        }

        this.IsDisposeRequested = true;
        this.PreparedToken = 0;

        MarkTerminalRetained(this.Active);
        MarkTerminalRetained(this.Prepared);
        MarkTerminalRetained(this.Retired);

        this.State = SlotControlState.RetiringActive;

        return true;
    }

    private static void RetireAndReleaseOwnership(in ResourceGenerationSetHandle handle)
    {
        IResourceGenerationOwner owner = handle.Owner;

        for (int i = 0; i < owner.ResourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(i);

            default(InvalidOperationException).ThrowIf(
                record.ReadLifecycle() is not ResourceGenerationState.Active,
                "The retiring resource generation is not active.");

            default(InvalidOperationException).ThrowIf(
                record.OwnerReferenceCount <= 0,
                "The retiring resource generation is not owned by the slot.");
        }

        for (int i = 0; i < owner.ResourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(i);

            if (record.TryRequestRetire())
            {
                record.ReleaseOwnerReference();
            }
        }
    }

    private static bool AreAllReleased(in ResourceGenerationSetHandle handle)
    {
        if (handle.IsEmpty)
        {
            return true;
        }

        IResourceGenerationOwner owner = handle.Owner;

        for (int i = 0; i < owner.ResourceCount; i++)
        {
            if (owner.GetResourceRecord(i).ReadLifecycle() is not ResourceGenerationState.Released)
            {
                return false;
            }
        }

        return true;
    }

    private static void MarkTerminalRetained(in ResourceGenerationSetHandle handle)
    {
        if (handle.IsEmpty)
        {
            return;
        }

        IResourceGenerationOwner owner = handle.Owner;

        for (int i = 0; i < owner.ResourceCount; i++)
        {
            _ = owner.GetResourceRecord(i).TryMarkTerminalRetained();
        }
    }

    private readonly ResourceGenerationSetId ActiveSetId()
    {
        return this.Active.IsEmpty ? default : this.Active.SetId;
    }
}
