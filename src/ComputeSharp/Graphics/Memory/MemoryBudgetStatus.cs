namespace ComputeSharp;

/// <summary>
/// The status of the video memory budget observed for a memory segment.
/// </summary>
public enum MemoryBudgetStatus : byte
{
    /// <summary>
    /// The budget could not be observed, so no allocation is admitted.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The budget was observed and can be used to admit allocations.
    /// </summary>
    Valid = 1,

    /// <summary>
    /// The memory segment is not active for the current adapter topology.
    /// </summary>
    Unsupported = 2,

    /// <summary>
    /// The budget could not be observed because the device was removed.
    /// </summary>
    DeviceLost = 3
}
