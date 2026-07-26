namespace ComputeSharp;

/// <summary>
/// A memory segment a graphics device can allocate native resources from.
/// </summary>
public enum GraphicsMemorySegment : byte
{
    /// <summary>
    /// The memory segment that is local to the adapter.
    /// </summary>
    Local = 0,

    /// <summary>
    /// The memory segment that is not local to the adapter.
    /// </summary>
    NonLocal = 1
}
