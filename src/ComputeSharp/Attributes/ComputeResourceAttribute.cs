using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that declares the access contract of a graphics resource parameter of a compute pipeline method.
/// </summary>
/// <param name="access">The access the compute queue declares over the resource.</param>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ComputeResourceAttribute(ComputeResourceAccess access) : Attribute
{
    /// <summary>
    /// Gets the access the compute queue declares over the resource.
    /// </summary>
    public ComputeResourceAccess Access { get; } = access;

    /// <summary>
    /// Gets or sets whether the resource is owned internally or shared with an external queue.
    /// </summary>
    public ComputeResourceSharing Sharing { get; set; }

    /// <summary>
    /// Gets or sets whether the resource may be aliased across multiple bindings.
    /// </summary>
    public ComputeResourceAliasing Aliasing { get; set; }
}
