namespace ComputeSharp;

/// <summary>
/// Indicates how the contents of an owned resource generation are recovered after a replacement.
/// </summary>
public enum ComputeResourceRecovery : byte
{
    /// <summary>
    /// The previous contents are discarded and not recovered.
    /// </summary>
    Discardable = 0,

    /// <summary>
    /// The contents are recreated from host data.
    /// </summary>
    RecreateFromHost = 1,

    /// <summary>
    /// The contents are recomputed from the compute pipeline.
    /// </summary>
    Recompute = 2,

    /// <summary>
    /// Only the storage capacity is recovered, without any content.
    /// </summary>
    CapacityOnly = 3
}
