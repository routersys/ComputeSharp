using System;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal struct SlotGate
{
    private const int PlanRegionCount = 3;

    private SpinLock exclusion;

    private SlotControlRecord control;

    private SlotResourcePlanStateRecord planState;

    private int[]? planStorage;

    private readonly int[] PlanStorage => this.planStorage ?? [];

    public bool IsAllocated
    {
        get
        {
            bool taken = false;

            try
            {
                this.exclusion.Enter(ref taken);

                return this.control.IsAllocated;
            }
            finally
            {
                if (taken)
                {
                    this.exclusion.Exit(useMemoryBarrier: true);
                }
            }
        }
    }

    public bool IsDisposeRequested
    {
        get
        {
            bool taken = false;

            try
            {
                this.exclusion.Enter(ref taken);

                return this.control.IsDisposeRequested;
            }
            finally
            {
                if (taken)
                {
                    this.exclusion.Exit(useMemoryBarrier: true);
                }
            }
        }
    }

    public bool IsUnbound
    {
        get
        {
            bool taken = false;

            try
            {
                this.exclusion.Enter(ref taken);

                return this.control.State is SlotControlState.Unbound;
            }
            finally
            {
                if (taken)
                {
                    this.exclusion.Exit(useMemoryBarrier: true);
                }
            }
        }
    }

    public bool IsDisposalComplete
    {
        get
        {
            bool taken = false;

            try
            {
                this.exclusion.Enter(ref taken);

                return this.control.State is SlotControlState.Unbound or SlotControlState.Disposed;
            }
            finally
            {
                if (taken)
                {
                    this.exclusion.Exit(useMemoryBarrier: true);
                }
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

        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            if (!this.control.TryBind())
            {
                return false;
            }

            this.planStorage = planStorage;
            this.planState = planState;

            return true;
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryPin(
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex,
        out ResourceGenerationPin pin)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return this.control.TryPin(setId, generationId, bindingEpoch, resourceIndex, out pin);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public ResourcePlanDecision Evaluate(in OwnedSlotDescriptor descriptor, ReadOnlySpan<int> requestedPlan)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.Evaluate(in this.control, this.PlanStorage, in this.planState, in descriptor, requestedPlan);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public ResourcePlanDecision EvaluateSharedTexture(in SharedTextureContractDescriptor descriptor, int requestedWidth, int requestedHeight)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.EvaluateSharedTexture(
                in this.control,
                this.PlanStorage,
                in this.planState,
                in descriptor,
                requestedWidth,
                requestedHeight);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken, ReadOnlySpan<int> requestedPlan)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryInstallPrepared(
                ref this.control,
                this.PlanStorage,
                in this.planState,
                prepared,
                preparedToken,
                requestedPlan);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryCommitReplacement(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryCommitReplacement(
                ref this.control,
                this.PlanStorage,
                in this.planState,
                expectedActiveSetId,
                expectedBindingEpoch,
                preparedToken,
                out detachedPrepared);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryAbortReplacement(ulong preparedToken, out ResourceGenerationSetHandle detachedPrepared)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryAbortReplacement(
                ref this.control,
                this.PlanStorage,
                in this.planState,
                preparedToken,
                out detachedPrepared);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryApplyLogicalUpdate(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ReadOnlySpan<int> requestedPlan)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryApplyLogicalUpdate(
                ref this.control,
                this.PlanStorage,
                in this.planState,
                expectedActiveSetId,
                expectedBindingEpoch,
                requestedPlan);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryTrim()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryTrim(ref this.control, this.PlanStorage, in this.planState);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public ResourceGenerationSetHandle RequestDispose()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.RequestDispose(ref this.control, this.PlanStorage, in this.planState);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryClearRetired(ResourceGenerationSetId expectedSetId)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryClearRetired(ref this.control, this.PlanStorage, in this.planState, expectedSetId);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryCompleteRetiringActive()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return SlotResourcePlanController.TryCompleteRetiringActive(ref this.control, this.PlanStorage, in this.planState);
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryMarkDeviceTerminal()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return this.control.TryMarkDeviceTerminal();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public ulong GetBindingEpoch()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return this.control.BindingEpoch;
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public void GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            activeSetId = this.control.ActiveSetId;
            bindingEpoch = this.control.BindingEpoch;
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public void GetMaintenanceHandles(
        out ResourceGenerationSetHandle active,
        out ResourceGenerationSetHandle prepared,
        out ResourceGenerationSetHandle retired,
        out bool isRetiringActive)
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            active = this.control.Active;
            prepared = this.control.Prepared;
            retired = this.control.Retired;
            isRetiringActive = this.control.State is SlotControlState.RetiringActive;
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public bool TryGetBinding<TResource>(int resourceIndex, out ComputeResourceBinding<TResource> binding)
        where TResource : class, IGraphicsResource
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            default(InvalidOperationException).ThrowIf(
                this.control.State is SlotControlState.Unbound,
                "The resource slot is not bound to a pipeline host.");

            if (!this.control.TryGetActiveResource(resourceIndex, out TResource resource, out ResourceGenerationId generationId))
            {
                binding = default;

                return false;
            }

            binding = new ComputeResourceBinding<TResource>(
                resource,
                this.control.ActiveSetId,
                generationId,
                this.control.BindingEpoch,
                resourceIndex);

            return true;
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    public void ThrowIfUnbound()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            default(InvalidOperationException).ThrowIf(
                this.control.State is SlotControlState.Unbound,
                "The resource slot is not bound to a pipeline host.");
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }
}
