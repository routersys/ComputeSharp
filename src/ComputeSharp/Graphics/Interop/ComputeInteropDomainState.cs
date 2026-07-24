namespace ComputeSharp.Interop;

internal enum ComputeInteropDomainState : byte
{
    Active = 0,
    DisposeRequested = 1,
    Poisoned = 2,
    TeardownStarted = 3,
    Terminal = 4,
    ReleasingNative = 5,
    Disposed = 6
}
