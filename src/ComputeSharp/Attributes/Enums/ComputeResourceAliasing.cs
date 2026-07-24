namespace ComputeSharp;

/// <summary>
/// Indicates whether a compute resource contract allows aliasing the same generation across multiple bindings.
/// </summary>
public enum ComputeResourceAliasing : byte
{
    /// <summary>
    /// The resource may not be bound to more than one ordinal within the same invocation.
    /// </summary>
    Disallow = 0,

    /// <summary>
    /// The resource may be bound to multiple ordinals when every conflicting contract also allows it.
    /// </summary>
    Allow = 1
}
