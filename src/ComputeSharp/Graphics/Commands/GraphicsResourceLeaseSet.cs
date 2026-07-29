using System;
using System.Collections.Concurrent;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Interop;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Commands;

internal sealed class GraphicsResourceLeaseSet
{
    private static readonly ConcurrentQueue<GraphicsResourceLeaseSet> LeaseSetQueue = [];

    private static volatile int queuedLeaseSetCount;

    private ReferenceTracker.Lease[] leases = new ReferenceTracker.Lease[16];

    private GraphicsResourceUsageEntry[] usages = new GraphicsResourceUsageEntry[16];

    private int count;

    private int usageCount;

    public static GraphicsResourceLeaseSet Rent()
    {
        if (LeaseSetQueue.TryDequeue(out GraphicsResourceLeaseSet? leaseSet))
        {
            _ = Interlocked.Decrement(ref queuedLeaseSetCount);
        }
        else
        {
            leaseSet = new GraphicsResourceLeaseSet();
        }

        return leaseSet;
    }

    public void Add(ReferenceTracker.Lease lease)
    {
        ReferenceTracker.Lease[] leases = this.leases;

        if (this.count == leases.Length)
        {
            Array.Resize(ref this.leases, leases.Length * 2);

            leases = this.leases;
        }

        leases[this.count++] = lease;
    }

    public Span<GraphicsResourceUsageEntry> GetResourceUsages()
    {
        return this.usages.AsSpan(0, this.usageCount);
    }

    public bool TryGetFinalState(ResourceGenerationId generation, out TrackedResourceState finalState)
    {
        Span<GraphicsResourceUsageEntry> usages = GetResourceUsages();

        for (int i = 0; i < usages.Length; i++)
        {
            if (usages[i].Generation == generation)
            {
                finalState = usages[i].FinalState;

                return true;
            }
        }

        finalState = TrackedResourceState.Unknown;

        return false;
    }

    public void RecordResourceUsage(
        in ResourceUsageBinding binding,
        ComputeResourceAccess access,
        TrackedResourceState firstState,
        TrackedResourceState finalState)
    {
        if (this.usageCount == this.usages.Length)
        {
            Array.Resize(ref this.usages, this.usages.Length * 2);
        }

        UsageSetPoolEntry usageSet = new()
        {
            StorageOffset = 0,
            Capacity = this.usages.Length,
            Count = this.usageCount
        };

        bool isTracked = ResourceUsageTracker.TryAddUsage(
            this.usages,
            ref usageSet,
            binding.Set,
            binding.ResourceIndex,
            binding.Generation,
            access,
            firstState,
            finalState,
            out _,
            out _);

        default(InvalidOperationException).ThrowIf(!isTracked, "The manual resource usage set has no entry left.");

        this.usageCount = usageSet.Count;
    }

    public void MarkComputeFence(ulong d3D12FenceValue)
    {
        ReferenceTracker.Lease[] leases = this.leases;

        for (int i = 0; i < this.count; i++)
        {
            if (leases[i].TrackedObject is ID3D12ComputeFenceTrackedResource resource)
            {
                resource.MarkComputeFence(d3D12FenceValue);
            }
        }
    }

    public void Release()
    {
        ReferenceTracker.Lease[] leases = this.leases;

        for (int i = 0; i < this.count; i++)
        {
            leases[i].Dispose();
        }

        GetResourceUsages().Clear();

        this.count = 0;
        this.usageCount = 0;

        if (Interlocked.Increment(ref queuedLeaseSetCount) < 16)
        {
            LeaseSetQueue.Enqueue(this);
        }
        else
        {
            _ = Interlocked.Decrement(ref queuedLeaseSetCount);
        }
    }
}
