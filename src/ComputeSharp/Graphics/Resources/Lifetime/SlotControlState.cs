namespace ComputeSharp.Resources.Lifetime;

internal enum SlotControlState : byte
{
    Unbound = 0,
    Active = 1,
    ReplacementPrepared = 2,
    DisposeWaitingForRetired = 3,
    RetiringActive = 4,
    Disposed = 5
}
