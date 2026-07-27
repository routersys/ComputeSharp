namespace ComputeSharp;

/// <summary>
/// The pixel format of a shared texture, as seen by both queues of a compute interop domain.
/// </summary>
public enum ExternalTextureFormat : byte
{
    /// <summary>
    /// Four 8 bit unsigned normalized channels, in blue, green, red and alpha order.
    /// </summary>
    Bgra8Unorm = 0
}
