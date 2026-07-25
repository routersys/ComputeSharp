using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A model representing all necessary info for a full generation pass for a canonical descriptor.
/// </summary>
/// <param name="Hierarchy">The hierarchy info for the annotated type.</param>
/// <param name="Descriptor">The canonical binary descriptor of the annotated type.</param>
internal sealed record PipelineDescriptorInfo(HierarchyInfo Hierarchy, EquatableArray<byte> Descriptor);
