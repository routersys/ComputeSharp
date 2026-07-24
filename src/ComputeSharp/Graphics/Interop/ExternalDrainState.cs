namespace ComputeSharp.Interop;

internal enum ExternalDrainState : byte
{
    None = 0,
    Requested = 1,
    Queued = 2,
    WaitingForDomainPermit = 3,
    WaitingForScheduler = 4,
    FenceIssued = 5,
    WaitingFence = 6,
    ExternalReleasePending = 7,
    Completed = 8,
    Faulted = 9
}
