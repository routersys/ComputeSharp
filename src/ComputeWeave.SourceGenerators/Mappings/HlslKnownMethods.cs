using System.Collections.Generic;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
partial class HlslKnownMethods
{
    /// <summary>
    /// Checks whether a method name, previously matched with <see cref="TryGetMappedName(string, out string?)"/>,
    /// maps to an HLSL barrier that every thread of a thread group has to reach.
    /// </summary>
    /// <param name="name">The fully qualified metadata name.</param>
    /// <returns>Whether the method maps to a barrier that synchronizes the whole thread group.</returns>
    /// <remarks>
    /// The three barriers without the group synchronization are deliberately not here. They order memory
    /// operations and do not wait, so a thread group missing some of its threads still gives them their
    /// meaning. It is waiting for the whole group that a partial group cannot give.
    /// </remarks>
    public static bool SynchronizesTheWholeThreadGroup(string name)
    {
        return name is
            "ComputeWeave.Hlsl.AllMemoryBarrierWithGroupSync" or
            "ComputeWeave.Hlsl.DeviceMemoryBarrierWithGroupSync" or
            "ComputeWeave.Hlsl.GroupMemoryBarrierWithGroupSync";
    }

    /// <inheritdoc/>
    private static partial Dictionary<string, string?> BuildKnownResourceSamplers()
    {
        return new()
        {
            [$"ComputeWeave.ReadOnlyTexture1D`2.Sample({typeof(float).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyTexture1D`1.Sample({typeof(float).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyNormalizedTexture1D`1.Sample({typeof(float).FullName})"] = null,
            [$"ComputeWeave.ReadOnlyTexture2D`2.Sample({typeof(float).FullName}, {typeof(float).FullName})"] = "float2",
            [$"ComputeWeave.ReadOnlyTexture2D`2.Sample({typeof(Float2).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyTexture2D`1.Sample({typeof(float).FullName}, {typeof(float).FullName})"] = "float2",
            [$"ComputeWeave.IReadOnlyTexture2D`1.Sample({typeof(Float2).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyNormalizedTexture2D`1.Sample({typeof(float).FullName}, {typeof(float).FullName})"] = "float2",
            [$"ComputeWeave.IReadOnlyNormalizedTexture2D`1.Sample({typeof(Float2).FullName})"] = null,
            [$"ComputeWeave.ReadOnlyTexture3D`2.Sample({typeof(float).FullName}, {typeof(float).FullName}, {typeof(float).FullName})"] = "float3",
            [$"ComputeWeave.ReadOnlyTexture3D`2.Sample({typeof(Float3).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyTexture3D`1.Sample({typeof(float).FullName}, {typeof(float).FullName}, {typeof(float).FullName})"] = "float3",
            [$"ComputeWeave.IReadOnlyTexture3D`1.Sample({typeof(Float3).FullName})"] = null,
            [$"ComputeWeave.IReadOnlyNormalizedTexture3D`1.Sample({typeof(float).FullName}, {typeof(float).FullName}, {typeof(float).FullName})"] = "float3",
            [$"ComputeWeave.IReadOnlyNormalizedTexture3D`1.Sample({typeof(Float3).FullName})"] = null
        };
    }
}