using System;

namespace ComputeSharp.Resources.Lifetime;

internal readonly struct ResourceCpuAccessScope(IResourceGenerationOwner owner, int resourceIndex) : IDisposable
{
    private readonly IResourceGenerationOwner? owner = owner;

    private readonly int resourceIndex = resourceIndex;

    public void Dispose()
    {
        this.owner?.GetResourceRecord(this.resourceIndex).ReleaseCpuReference();
    }
}
