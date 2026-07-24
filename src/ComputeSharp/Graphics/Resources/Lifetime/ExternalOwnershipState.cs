namespace ComputeSharp.Resources.Lifetime;

internal enum ExternalOwnershipState : byte
{
    ComputeAvailable = 0,
    ExternalAvailable = 1,
    AcquireSignalEnqueued = 2,
    ComputeExecutionIssued = 3,
    ReleaseSignalEnqueued = 4,
    Faulted = 5
}
