namespace ComputeWeave.Interop;

/// <summary>
/// Indicates how acquiring a native resource reference relates to the work the runtime has already submitted.
/// </summary>
public enum NativeResourceAcquisition : byte
{
    /// <summary>
    /// The reference is acquired without waiting for any submitted work to complete.
    /// </summary>
    Immediate = 0,

    /// <summary>
    /// The reference is acquired after every submission that already used the resource generation has completed.
    /// </summary>
    /// <remarks>
    /// This blocks the calling thread until the GPU reaches the observed completion points. Work submitted
    /// after the reference is acquired is not waited for.
    /// </remarks>
    AfterPendingWork = 1
}
