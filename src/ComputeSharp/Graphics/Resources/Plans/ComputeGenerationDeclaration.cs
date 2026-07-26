using ComputeSharp.Memory;

namespace ComputeSharp.Resources.Plans;

internal struct ComputeGenerationDeclaration
{
    public ComputeGenerationShape Shape;

    public int Width;

    public int Height;

    public MemoryPlacement Placement;

    public ulong SizeInBytes;

    public GraphicsCommittedResourceDescription Description;

    public readonly bool IsSameDeclaration(in ComputeGenerationDeclaration other)
    {
        return this.Shape == other.Shape && this.Width == other.Width && this.Height == other.Height;
    }
}
