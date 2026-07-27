using System;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal sealed class ResourceIdentityAllocator
{
    private readonly GraphicsDevice device;

    private ulong nextResourceId;

    private ulong nextGenerationId;

    private ulong nextGenerationSetId;

    public ResourceIdentityAllocator(GraphicsDevice device)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        this.device = device;
    }

    public ResourceId CreateResourceId()
    {
        return new ResourceId(CreateIdentity(ref this.nextResourceId, "resource identity"));
    }

    public ResourceGenerationId CreateGenerationId()
    {
        return new ResourceGenerationId(CreateIdentity(ref this.nextGenerationId, "resource generation identity"));
    }

    public ResourceGenerationSetId CreateGenerationSetId()
    {
        return new ResourceGenerationSetId(CreateIdentity(ref this.nextGenerationSetId, "resource generation set identity"));
    }

    private ulong CreateIdentity(ref ulong sequence, string name)
    {
        ulong value = Interlocked.Increment(ref sequence);

        if (value == 0)
        {
            this.device.ThrowTerminalSequenceExhaustion(name);
        }

        return value;
    }
}
