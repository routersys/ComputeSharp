namespace ComputeSharp;

/// <summary>
/// Indicates how a shared texture is used by the external queue.
/// </summary>
public enum ExternalTextureUsage : byte
{
    /// <summary>
    /// The texture is used as a sampled input.
    /// </summary>
    Sampled = 0,

    /// <summary>
    /// The texture is used as a render target.
    /// </summary>
    RenderTarget = 1
}
