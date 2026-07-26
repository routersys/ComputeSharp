namespace ComputeSharp;

/// <summary>
/// A memory grant issued by an external budget broker for a single memory segment.
/// </summary>
public readonly struct GraphicsMemoryGrant
{
    /// <summary>
    /// Gets whether the grant carries a limit. When this is <see langword="false"/>, <see cref="LimitBytes"/> is ignored.
    /// </summary>
    public bool HasLimit { get; init; }

    /// <summary>
    /// Gets the maximum number of bytes the device is allowed to own in the granted segment.
    /// </summary>
    public ulong LimitBytes { get; init; }

    /// <summary>
    /// Gets the version of the grant, which is monotonically non decreasing per client and segment.
    /// </summary>
    public ulong Version { get; init; }
}
