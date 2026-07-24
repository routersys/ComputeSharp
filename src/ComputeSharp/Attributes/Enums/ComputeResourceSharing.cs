namespace ComputeSharp;

/// <summary>
/// Indicates whether a compute resource parameter is owned internally or shared with an external queue.
/// </summary>
public enum ComputeResourceSharing : byte
{
    /// <summary>
    /// The resource is only used from within the compute queue.
    /// </summary>
    Internal = 0,

    /// <summary>
    /// The resource is shared with an external queue and bound through a generation binding.
    /// </summary>
    External = 1
}
