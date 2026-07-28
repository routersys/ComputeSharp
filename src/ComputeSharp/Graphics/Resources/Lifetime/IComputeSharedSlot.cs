using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal interface IComputeSharedSlot
{
    bool IsDisposalComplete { get; }

    bool TryBind(
        InteropResourceSetRuntime runtime,
        SlotOrdinal ordinal,
        int[] planStorage,
        in SlotResourcePlanStateRecord planState);

    void RequestDispose();

    void RunMaintenance();

    bool TryGetPendingDrainFence(out FencePoint fence);

    void MarkTerminalRetained();

    void ReleaseTerminalGenerations();
}
