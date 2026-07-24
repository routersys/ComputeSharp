using System;

namespace ComputeSharp;

/// <summary>
/// An attribute that marks a <see langword="sealed partial class"/> as a compute pipeline host.
/// </summary>
/// <param name="deviceFieldName">The name of the <see cref="GraphicsDevice"/> field owned by the host.</param>
/// <param name="maximumConcurrentInvocations">The maximum number of concurrent pipeline invocations.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ComputePipelineHostAttribute(string deviceFieldName, int maximumConcurrentInvocations) : Attribute
{
    /// <summary>
    /// Gets the name of the <see cref="GraphicsDevice"/> field owned by the host.
    /// </summary>
    public string DeviceFieldName { get; } = deviceFieldName;

    /// <summary>
    /// Gets the maximum number of concurrent pipeline invocations.
    /// </summary>
    public int MaximumConcurrentInvocations { get; } = maximumConcurrentInvocations;
}
