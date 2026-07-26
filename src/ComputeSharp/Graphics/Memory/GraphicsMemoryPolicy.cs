namespace ComputeSharp;

/// <summary>
/// The memory policy a graphics device admits native resource allocations against.
/// </summary>
public readonly struct GraphicsMemoryPolicy
{
    /// <summary>
    /// Gets the broker to register a budget client with, or <see langword="null"/> to admit allocations without a broker.
    /// </summary>
    public IGraphicsMemoryBudgetBroker? BudgetBroker { get; init; }

    /// <summary>
    /// Gets the hard limit on the bytes owned in the local segment, or <see langword="null"/> for no dedicated limit.
    /// </summary>
    public ulong? LocalOwnedHardLimitBytes { get; init; }

    /// <summary>
    /// Gets the hard limit on the bytes owned in the non local segment, or <see langword="null"/> for no dedicated limit.
    /// </summary>
    public ulong? NonLocalOwnedHardLimitBytes { get; init; }
}
