using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that marks a compute pipeline method as an external interop round-trip.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComputeInteropAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="ComputeInteropAttribute"/> instance.
    /// </summary>
    public ComputeInteropAttribute()
    {
    }
}
