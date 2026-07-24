using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that declares the access contract of a graphics resource member owned or borrowed by a compute pipeline host or resource group.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ComputePipelineResourceAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="ComputePipelineResourceAttribute"/> instance with the specified parameters.
    /// </summary>
    /// <param name="access">The access the compute queue declares over the resource.</param>
    public ComputePipelineResourceAttribute(ComputeResourceAccess access)
    {
        Access = access;
    }

    /// <summary>
    /// Creates a new <see cref="ComputePipelineResourceAttribute"/> instance with the specified parameters.
    /// </summary>
    /// <param name="access">The access the compute queue declares over the resource.</param>
    /// <param name="recovery">The recovery class of the owned resource slot.</param>
    public ComputePipelineResourceAttribute(ComputeResourceAccess access, ComputeResourceRecovery recovery)
    {
        Access = access;
        HasRecovery = true;
        Recovery = recovery;
    }

    /// <summary>
    /// Gets the access the compute queue declares over the resource.
    /// </summary>
    public ComputeResourceAccess Access { get; }

    /// <summary>
    /// Gets whether a recovery class was declared for the resource.
    /// </summary>
    public bool HasRecovery { get; }

    /// <summary>
    /// Gets the recovery class of the owned resource slot.
    /// </summary>
    public ComputeResourceRecovery Recovery { get; }
}
