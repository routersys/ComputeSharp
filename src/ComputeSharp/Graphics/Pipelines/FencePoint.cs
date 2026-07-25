namespace ComputeSharp;

/// <summary>
/// Identifies a point on the timeline of a specific GPU queue.
/// </summary>
public readonly struct FencePoint
{
    /// <summary>
    /// Creates a new <see cref="FencePoint"/> instance with the specified parameters.
    /// </summary>
    /// <param name="queue">The queue the fence point belongs to.</param>
    /// <param name="value">The fence value on the queue timeline.</param>
    internal FencePoint(ComputeQueueKind queue, ulong value)
    {
        Queue = queue;
        Value = value;
    }

    /// <summary>
    /// Gets the queue the fence point belongs to.
    /// </summary>
    public ComputeQueueKind Queue { get; }

    /// <summary>
    /// Gets the fence value on the queue timeline.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Gets a <see cref="FencePoint"/> representing a completed no-op.
    /// </summary>
    public static FencePoint None => default;

    /// <summary>
    /// Gets whether the fence point represents a completed no-op.
    /// </summary>
    public bool IsNone => Value == 0;
}
