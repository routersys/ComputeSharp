using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that marks a <see langword="sealed partial class"/> as a compute interop resource set.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ComputeInteropResourceSetAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="ComputeInteropResourceSetAttribute"/> instance.
    /// </summary>
    public ComputeInteropResourceSetAttribute()
    {
    }
}
