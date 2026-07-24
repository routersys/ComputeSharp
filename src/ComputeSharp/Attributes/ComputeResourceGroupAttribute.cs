using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that marks a <see langword="sealed partial class"/> as a compute resource group.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ComputeResourceGroupAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="ComputeResourceGroupAttribute"/> instance.
    /// </summary>
    public ComputeResourceGroupAttribute()
    {
    }
}
