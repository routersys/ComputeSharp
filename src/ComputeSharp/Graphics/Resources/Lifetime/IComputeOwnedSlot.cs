using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal interface IComputeOwnedSlot
{
    bool IsDisposalComplete { get; }

    bool TryBind(int[] planStorage, in SlotResourcePlanStateRecord planState);

    void RequestDispose();

    void ThrowIfUnbound();

    void RunMaintenance();

    ResourcePlanDecision Evaluate(in OwnedSlotDescriptor descriptor, ReadOnlySpan<int> requestedPlan);

    void GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch);

    bool TryApplyLogicalUpdate(ResourceGenerationSetId expectedActiveSetId, ulong expectedBindingEpoch, ReadOnlySpan<int> requestedPlan);

    bool TryInstallPrepared(ResourceGenerationSetHandle prepared, ulong preparedToken, ReadOnlySpan<int> requestedPlan);

    bool TryCommitReplacement(
        ResourceGenerationSetId expectedActiveSetId,
        ulong expectedBindingEpoch,
        ulong preparedToken,
        out ResourceGenerationSetHandle detachedPrepared);

    bool TryAbortReplacement(ulong preparedToken, out ResourceGenerationSetHandle detachedPrepared);

    bool TryGetBinding<TResource>(int resourceIndex, out ComputeResourceBinding<TResource> binding)
        where TResource : class, IGraphicsResource;
}
