namespace ComputeSharp;

/// <summary>
/// The description of a shared texture the runtime asks an external interop provider to open.
/// </summary>
public readonly struct ExternalTextureDescriptor
{
    /// <summary>
    /// Gets the width of the shared texture.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the height of the shared texture.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the pixel format of the shared texture.
    /// </summary>
    public ExternalTextureFormat Format { get; init; }

    /// <summary>
    /// Gets the usage of the shared texture on the external queue.
    /// </summary>
    public ExternalTextureUsage ExternalUsage { get; init; }

    /// <summary>
    /// Gets the alpha mode of the shared texture.
    /// </summary>
    public ComputeAlphaMode AlphaMode { get; init; }
}
