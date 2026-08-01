using System.Collections.Generic;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <inheritdoc/>
partial class HlslKnownMethods
{
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