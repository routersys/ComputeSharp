namespace ComputeSharp;

/// <summary>
/// Indicates how the alpha channel of a shared texture is interpreted.
/// </summary>
public enum ComputeAlphaMode : byte
{
    /// <summary>
    /// The alpha channel is ignored.
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// The color channels are premultiplied by the alpha channel.
    /// </summary>
    Premultiplied = 1,

    /// <summary>
    /// The color channels are not premultiplied by the alpha channel.
    /// </summary>
    Straight = 2
}
