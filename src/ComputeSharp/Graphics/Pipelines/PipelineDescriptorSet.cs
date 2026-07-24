namespace ComputeSharp.Graphics.Pipelines;

internal sealed class PipelineDescriptorSet(
    DescriptorKind kind,
    PipelineHostDescriptor host,
    InteropResourceSetDescriptor resourceSet)
{
    public DescriptorKind Kind { get; } = kind;

    public PipelineHostDescriptor Host { get; } = host;

    public InteropResourceSetDescriptor ResourceSet { get; } = resourceSet;
}
