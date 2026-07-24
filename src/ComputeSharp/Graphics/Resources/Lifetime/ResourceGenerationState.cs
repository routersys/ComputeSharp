namespace ComputeSharp.Resources.Lifetime;

internal enum ResourceGenerationState : byte
{
    Constructing = 0,
    Active = 1,
    RetireRequested = 2,
    RetiredPending = 3,
    RetiredReady = 4,
    Releasing = 5,
    Released = 6,
    Faulted = 7,
    TerminalRetained = 8
}
