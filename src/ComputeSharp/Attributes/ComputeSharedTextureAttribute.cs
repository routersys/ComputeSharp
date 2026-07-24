using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that declares the contract of a shared texture field in a compute interop resource set.
/// </summary>
/// <param name="resizePolicy">The resize policy of the shared texture.</param>
/// <param name="computeAccess">The access the compute queue declares over the shared texture.</param>
/// <param name="externalAccess">The access the external queue declares over the shared texture.</param>
/// <param name="externalUsage">The usage of the shared texture on the external queue.</param>
/// <param name="alphaMode">The alpha mode of the shared texture.</param>
/// <param name="initialOwner">The queue that initially owns the shared texture.</param>
/// <param name="recovery">The recovery class of the shared texture.</param>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ComputeSharedTextureAttribute(
    ComputeResourceResizePolicy resizePolicy,
    ComputeResourceAccess computeAccess,
    ExternalResourceAccess externalAccess,
    ExternalTextureUsage externalUsage,
    ComputeAlphaMode alphaMode,
    ComputeSharedTextureInitialOwner initialOwner,
    ComputeResourceRecovery recovery) : Attribute
{
    /// <summary>
    /// Gets the resize policy of the shared texture.
    /// </summary>
    public ComputeResourceResizePolicy ResizePolicy { get; } = resizePolicy;

    /// <summary>
    /// Gets the access the compute queue declares over the shared texture.
    /// </summary>
    public ComputeResourceAccess ComputeAccess { get; } = computeAccess;

    /// <summary>
    /// Gets the access the external queue declares over the shared texture.
    /// </summary>
    public ExternalResourceAccess ExternalAccess { get; } = externalAccess;

    /// <summary>
    /// Gets the usage of the shared texture on the external queue.
    /// </summary>
    public ExternalTextureUsage ExternalUsage { get; } = externalUsage;

    /// <summary>
    /// Gets the alpha mode of the shared texture.
    /// </summary>
    public ComputeAlphaMode AlphaMode { get; } = alphaMode;

    /// <summary>
    /// Gets the queue that initially owns the shared texture.
    /// </summary>
    public ComputeSharedTextureInitialOwner InitialOwner { get; } = initialOwner;

    /// <summary>
    /// Gets the recovery class of the shared texture.
    /// </summary>
    public ComputeResourceRecovery Recovery { get; } = recovery;
}
