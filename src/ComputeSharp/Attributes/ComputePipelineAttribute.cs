using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that marks a private instance method as a top-level compute pipeline submission entry point.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComputePipelineAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="ComputePipelineAttribute"/> instance.
    /// </summary>
    public ComputePipelineAttribute()
    {
    }
}
