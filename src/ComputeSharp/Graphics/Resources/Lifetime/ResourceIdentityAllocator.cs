using System.Threading;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal sealed class ResourceIdentityAllocator
{
    private ulong nextResourceId;

    private ulong nextGenerationId;

    private ulong nextGenerationSetId;

    public ResourceId CreateResourceId()
    {
        return new ResourceId(Interlocked.Increment(ref this.nextResourceId));
    }

    public ResourceGenerationId CreateGenerationId()
    {
        return new ResourceGenerationId(Interlocked.Increment(ref this.nextGenerationId));
    }

    public ResourceGenerationSetId CreateGenerationSetId()
    {
        return new ResourceGenerationSetId(Interlocked.Increment(ref this.nextGenerationSetId));
    }
}
