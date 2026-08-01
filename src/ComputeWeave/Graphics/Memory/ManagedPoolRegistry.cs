using System;
using System.Collections.Generic;
using System.Threading;

namespace ComputeWeave.Memory;

internal interface IManagedSurplusPool
{
    int SurplusCount { get; }

    int TrimSurplus();
}

internal sealed class ManagedPoolRegistry
{
    private readonly List<IManagedSurplusPool> pools = [];

    private readonly Lock gate = new();

    public void Register(IManagedSurplusPool pool)
    {
        default(ArgumentNullException).ThrowIfNull(pool);

        lock (this.gate)
        {
            default(InvalidOperationException).ThrowIf(this.pools.Contains(pool), "The managed pool is already registered.");

            this.pools.Add(pool);
        }
    }

    public int GetSurplusCount()
    {
        int count = 0;

        lock (this.gate)
        {
            foreach (IManagedSurplusPool pool in this.pools)
            {
                count = checked(count + pool.SurplusCount);
            }
        }

        return count;
    }

    public int TrimSurplus()
    {
        int count = 0;

        lock (this.gate)
        {
            foreach (IManagedSurplusPool pool in this.pools)
            {
                count = checked(count + pool.TrimSurplus());
            }
        }

        return count;
    }
}
