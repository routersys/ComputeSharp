namespace ComputeSharp;

/// <summary>
/// Indicates the access a compute resource contract declares over a bound graphics resource.
/// </summary>
public enum ComputeResourceAccess : byte
{
    /// <summary>
    /// The resource is only read from the compute queue.
    /// </summary>
    Read = 0,

    /// <summary>
    /// The resource is only written from the compute queue.
    /// </summary>
    Write = 1,

    /// <summary>
    /// The resource is both read from and written to from the compute queue.
    /// </summary>
    ReadWrite = 2
}
