namespace ComputeSharp;

/// <summary>
/// Indicates which queue initially owns a shared texture.
/// </summary>
public enum ComputeSharedTextureInitialOwner : byte
{
    /// <summary>
    /// The compute queue initially owns the shared texture.
    /// </summary>
    Compute = 0,

    /// <summary>
    /// The external queue initially owns the shared texture.
    /// </summary>
    External = 1
}
