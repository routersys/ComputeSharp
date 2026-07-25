using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Commands.Interop;

internal sealed unsafe class PipelineCommandListPool(ID3D12Device* d3D12Device, D3D12_COMMAND_LIST_TYPE d3D12CommandListType) : IDisposable
{
    private readonly List<PipelineCommandListPartition> partitions = [];

    private readonly ID3D12Device* d3D12Device = d3D12Device;

    private bool isDisposed;

    public int PartitionCount
    {
        get
        {
            lock (this.partitions)
            {
                return this.partitions.Count;
            }
        }
    }

    public PipelineCommandListPartition CreatePartition(int entryCount)
    {
        lock (this.partitions)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The command list pool has been disposed.");
        }

        PipelineCommandListPartition partition = new(this.d3D12Device, d3D12CommandListType, entryCount);

        lock (this.partitions)
        {
            if (this.isDisposed)
            {
                partition.Dispose();

                default(InvalidOperationException).ThrowIf(true, "The command list pool has been disposed.");
            }

            this.partitions.Add(partition);
        }

        return partition;
    }

    public void DestroyPartition(PipelineCommandListPartition partition)
    {
        default(ArgumentNullException).ThrowIfNull(partition);

        lock (this.partitions)
        {
            default(InvalidOperationException).ThrowIf(partition.HasRentedEntries, "The command list partition still has rented entries.");
            default(InvalidOperationException).ThrowIf(!this.partitions.Remove(partition), "The command list partition is not owned by the pool.");
        }

        partition.Dispose();
    }

    public void Dispose()
    {
        PipelineCommandListPartition[] pendingPartitions;

        lock (this.partitions)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            pendingPartitions = [.. this.partitions];

            this.partitions.Clear();
        }

        ExceptionDispatchInfo? failure = null;

        foreach (PipelineCommandListPartition partition in pendingPartitions)
        {
            try
            {
                partition.Dispose();
            }
            catch (Exception e)
            {
                failure ??= ExceptionDispatchInfo.Capture(e);
            }
        }

        failure?.Throw();
    }
}
