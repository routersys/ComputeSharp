using System;
using ComputeWeave.Graphics.Pipelines;

namespace ComputeWeave.Resources.Lifetime;

internal interface IComputeOwnedSlot : IComputeGenerationPinSource
{
    bool IsDisposalComplete { get; }

    bool TryBind(DeviceRegistrationRegistry registry, int[] planStorage, in SlotResourcePlanStateRecord planState);

    void RequestDispose();

    void ThrowIfUnbound();

    void RunMaintenance();

    bool TryTrim();

    bool TryGetTrimCandidate(out SlotTrimCandidate candidate);

    void GetGenerationCounts(ref int activeCount, ref int retiredCount);

    void MarkTerminalRetained();

    void ReleaseTerminalGenerations();

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
