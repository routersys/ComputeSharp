namespace ComputeWeave;

/// <summary>
/// Identifies the outcome of a compute submission.
/// </summary>
public enum ComputeSubmissionStatus : byte
{
    /// <summary>
    /// The submission has completed on its queue.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The submission has not completed on its queue yet.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The submission cannot complete, as its device reached a terminal state.
    /// </summary>
    Faulted = 2
}
