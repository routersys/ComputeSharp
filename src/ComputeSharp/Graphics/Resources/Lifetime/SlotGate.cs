using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal sealed class SlotGate
{
    private const int PlanRegionCount = 3;

    private readonly object gate = new();

    private SlotControlRecord control;

    private SlotResourcePlanStateRecord planState;

    private int[] planStorage = [];

    public bool IsAllocated
    {
        get
        {
            lock (this.gate)
            {
                return this.control.IsAllocated;
            }
        }
    }

    public bool IsDisposeRequested
    {
        get
        {
            lock (this.gate)
            {
                return this.control.IsDisposeRequested;
            }
        }
    }

    public bool IsUnbound
    {
        get
        {
            lock (this.gate)
            {
                return this.control.State is SlotControlState.Unbound;
            }
        }
    }

    public bool IsDisposalComplete
    {
        get
        {
            lock (this.gate)
            {
                return this.control.State is SlotControlState.Unbound or SlotControlState.Disposed;
            }
        }
    }

    public bool TryBind(int[] planStorage, in SlotResourcePlanStateRecord planState)
    {
        default(ArgumentNullException).ThrowIfNull(planStorage);
        default(ArgumentOutOfRangeException).ThrowIfNegative(planState.StorageOffset);
        default(ArgumentOutOfRangeException).ThrowIfNegative(planState.FieldCount);
        default(ArgumentException).ThrowIf(
            checked(planState.StorageOffset + (PlanRegionCount * planState.FieldCount)) > planStorage.Length,
            nameof(planState));

        lock (this.gate)
        {
            if (!this.control.TryBind())
            {
                return false;
            }

            this.planStorage = planStorage;
            this.planState = planState;

            return true;
        }
    }

    public bool TryPin(
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex,
        out ResourceGenerationPin pin)
    {
        lock (this.gate)
        {
            return this.control.TryPin(setId, generationId, bindingEpoch, resourceIndex, out pin);
        }
    }

    public ResourcePlanDecision Evaluate(in OwnedSlotDescriptor descriptor, ReadOnlySpan<int> requestedPlan)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.Evaluate(in this.control, this.planStorage, in this.planState, in descriptor, requestedPlan);
        }
    }

    public ResourcePlanDecision EvaluateSharedTexture(in SharedTextureContractDescriptor descriptor, int requestedWidth, int requestedHeight)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.EvaluateSharedTexture(
                in this.control,
                this.planStorage,
                in this.planState,
                in descriptor,
                requestedWidth,
                requestedHeight);
        }
    }

    public bool TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken, ReadOnlySpan<int> requestedPlan)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryInstallPrepared(
                ref this.control,
                this.planStorage,
                in this.planState,
                prepared,
                preparedToken,
                requestedPlan);
        }
    }

    public bool TryCommitReplacement(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryCommitReplacement(
                ref this.control,
                this.planStorage,
                in this.planState,
                expectedActiveSetId,
                expectedBindingEpoch,
                preparedToken,
                out detachedPrepared);
        }
    }

    public bool TryAbortReplacement(ulong preparedToken, out ResourceGenerationSetHandle detachedPrepared)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryAbortReplacement(
                ref this.control,
                this.planStorage,
                in this.planState,
                preparedToken,
                out detachedPrepared);
        }
    }

    public bool TryApplyLogicalUpdate(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ReadOnlySpan<int> requestedPlan)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryApplyLogicalUpdate(
                ref this.control,
                this.planStorage,
                in this.planState,
                expectedActiveSetId,
                expectedBindingEpoch,
                requestedPlan);
        }
    }

    public bool TryTrim()
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryTrim(ref this.control, this.planStorage, in this.planState);
        }
    }

    public ResourceGenerationSetHandle RequestDispose()
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.RequestDispose(ref this.control, this.planStorage, in this.planState);
        }
    }

    public bool TryClearRetired(ResourceGenerationSetId expectedSetId)
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryClearRetired(ref this.control, this.planStorage, in this.planState, expectedSetId);
        }
    }

    public bool TryCompleteRetiringActive()
    {
        lock (this.gate)
        {
            return SlotResourcePlanController.TryCompleteRetiringActive(ref this.control, this.planStorage, in this.planState);
        }
    }

    public bool TryMarkDeviceTerminal()
    {
        lock (this.gate)
        {
            return this.control.TryMarkDeviceTerminal();
        }
    }

    public ulong GetBindingEpoch()
    {
        lock (this.gate)
        {
            return this.control.BindingEpoch;
        }
    }
}
