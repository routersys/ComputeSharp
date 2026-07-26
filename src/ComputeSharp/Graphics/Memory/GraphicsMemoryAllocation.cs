using System;

namespace ComputeSharp.Memory;

internal struct GraphicsMemoryAllocation(MemoryAllocationCoordinator coordinator, MemoryPlacement placement, ulong bytes) : IDisposable
{
    private MemoryAllocationCoordinator? coordinator = coordinator;

    public MemoryPlacement Placement { get; } = placement;

    public ulong Bytes { get; } = bytes;

    public void Dispose()
    {
        if (this.coordinator is not MemoryAllocationCoordinator owner)
        {
            return;
        }

        this.coordinator = null;

        owner.ReleaseOwned(Placement, Bytes);
    }
}
