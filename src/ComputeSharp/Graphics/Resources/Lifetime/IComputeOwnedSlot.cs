namespace ComputeSharp.Resources.Lifetime;

internal interface IComputeOwnedSlot
{
    bool IsDisposalComplete { get; }

    bool TryBind(int[] planStorage, in SlotResourcePlanStateRecord planState);

    void RequestDispose();
}
