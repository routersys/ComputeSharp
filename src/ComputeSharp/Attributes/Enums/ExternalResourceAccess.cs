namespace ComputeSharp;

/// <summary>
/// Indicates the access an external queue declares over a shared resource.
/// </summary>
public enum ExternalResourceAccess : byte
{
    /// <summary>
    /// The resource is only read from the external queue.
    /// </summary>
    Read = 0,

    /// <summary>
    /// The resource is only written from the external queue.
    /// </summary>
    Write = 1,

    /// <summary>
    /// The resource is both read from and written to from the external queue.
    /// </summary>
    ReadWrite = 2
}
