using System;

namespace ComputeSharp.Graphics.Pipelines;

[Flags]
internal enum PipelineFlags : uint
{
    None = 0,
    InteropRoundTrip = 1u << 0,
    UsesReadBack = 1u << 1
}
