namespace ComputeSharp;

/// <summary>
/// Indicates how the physical dimensions of a resource generation react to a requested size.
/// </summary>
public enum ComputeResourceResizePolicy : byte
{
    /// <summary>
    /// The physical dimensions match the requested dimensions exactly.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// The physical dimensions are at least the requested dimensions and only grow on replacement.
    /// </summary>
    GrowOnly = 1
}
