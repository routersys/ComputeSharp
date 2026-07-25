namespace ComputeSharp;

/// <summary>
/// Identifies the GPU queue a fence point belongs to.
/// </summary>
public enum ComputeQueueKind : byte
{
    /// <summary>
    /// The fence point does not belong to any queue.
    /// </summary>
    None = 0,

    /// <summary>
    /// The fence point belongs to the compute queue.
    /// </summary>
    Compute = 1,

    /// <summary>
    /// The fence point belongs to the copy queue.
    /// </summary>
    Copy = 2
}
