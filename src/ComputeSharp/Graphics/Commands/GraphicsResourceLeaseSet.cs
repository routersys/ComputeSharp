using System;
using System.Collections.Concurrent;
using System.Threading;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Interop;

namespace ComputeSharp.Graphics.Commands;

internal sealed class GraphicsResourceLeaseSet
{
    private static readonly ConcurrentQueue<GraphicsResourceLeaseSet> LeaseSetQueue = [];

    private static volatile int queuedLeaseSetCount;

    private ReferenceTracker.Lease[] leases = new ReferenceTracker.Lease[16];

    private int count;

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

        this.count = 0;

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
