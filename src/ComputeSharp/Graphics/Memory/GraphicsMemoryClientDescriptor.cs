namespace ComputeSharp;

/// <summary>
/// A description of the graphics device a budget broker client is registered for.
/// </summary>
public readonly struct GraphicsMemoryClientDescriptor
{
    /// <summary>
    /// Gets the locally unique identifier of the adapter the device was created from.
    /// </summary>
    public long AdapterLuid { get; init; }

    /// <summary>
    /// Gets the index of the adapter node the device allocates from.
    /// </summary>
    public uint NodeIndex { get; init; }
}
