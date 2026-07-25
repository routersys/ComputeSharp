using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Commands.Interop;

internal readonly unsafe struct PipelineCommandListRental(int index, ID3D12GraphicsCommandList* d3D12CommandList, ID3D12CommandAllocator* d3D12CommandAllocator)
{
    public int Index { get; } = index;

    public ID3D12GraphicsCommandList* D3D12CommandList { get; } = d3D12CommandList;

    public ID3D12CommandAllocator* D3D12CommandAllocator { get; } = d3D12CommandAllocator;
}

internal sealed unsafe class PipelineCommandListPartition : IDisposable
{
    private struct Entry
    {
        public ID3D12GraphicsCommandList* D3D12CommandList;

        public ID3D12CommandAllocator* D3D12CommandAllocator;

        public bool IsRented;
    }

    private readonly Entry[] entries;

    private readonly int[] freeIndices;

    private int head;

    private int tail;

    private int size;

    private bool isDisposed;

    public PipelineCommandListPartition(ID3D12Device* d3D12Device, D3D12_COMMAND_LIST_TYPE d3D12CommandListType, int entryCount)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(entryCount);

        this.entries = new Entry[entryCount];
        this.freeIndices = new int[entryCount];
        this.head = 0;
        this.tail = 0;
        this.size = entryCount;

        int createdCount = 0;

        try
        {
            for (; createdCount < entryCount; createdCount++)
            {
                using ComPtr<ID3D12CommandAllocator> d3D12CommandAllocator = d3D12Device->CreateCommandAllocator(d3D12CommandListType);
                using ComPtr<ID3D12GraphicsCommandList> d3D12CommandList = d3D12Device->CreateCommandList(d3D12CommandListType, d3D12CommandAllocator.Get(), null);

                d3D12CommandList.Get()->Close().Assert();

                this.entries[createdCount].D3D12CommandAllocator = d3D12CommandAllocator.Detach();
                this.entries[createdCount].D3D12CommandList = d3D12CommandList.Detach();
                this.freeIndices[createdCount] = createdCount;
            }
        }
        catch
        {
            ReleaseEntries(createdCount);

            throw;
        }
    }

    public int Capacity => this.entries.Length;

    public int AvailableCount
    {
        get
        {
            lock (this.entries)
            {
                return this.size;
            }
        }
    }

    public void Rent(ID3D12PipelineState* d3D12PipelineState, out PipelineCommandListRental rental)
    {
        int index;

        lock (this.entries)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The command list partition has been disposed.");
            default(InvalidOperationException).ThrowIf(this.size <= 0, "The command list partition has no reserved entry left.");

            index = this.freeIndices[this.head++];

            if (this.head == this.freeIndices.Length)
            {
                this.head = 0;
            }

            this.size--;
            this.entries[index].IsRented = true;
        }

        ID3D12CommandAllocator* d3D12CommandAllocator = this.entries[index].D3D12CommandAllocator;
        ID3D12GraphicsCommandList* d3D12CommandList = this.entries[index].D3D12CommandList;

        d3D12CommandAllocator->Reset().Assert();
        d3D12CommandList->Reset(d3D12CommandAllocator, d3D12PipelineState).Assert();

        rental = new PipelineCommandListRental(index, d3D12CommandList, d3D12CommandAllocator);
    }

    public void Return(int index, bool isCommandListClosed)
    {
        lock (this.entries)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The command list partition has been disposed.");
            default(ArgumentOutOfRangeException).ThrowIfNotInRange(index, 0, this.entries.Length);
            default(InvalidOperationException).ThrowIf(!this.entries[index].IsRented, "The command list entry is not rented.");

            this.entries[index].IsRented = false;
        }

        if (!isCommandListClosed)
        {
            this.entries[index].D3D12CommandList->Close().Assert();
        }

        lock (this.entries)
        {
            this.freeIndices[this.tail++] = index;

            if (this.tail == this.freeIndices.Length)
            {
                this.tail = 0;
            }

            this.size++;
        }
    }

    public bool HasRentedEntries
    {
        get
        {
            lock (this.entries)
            {
                return this.size != this.entries.Length;
            }
        }
    }

    public void Dispose()
    {
        lock (this.entries)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
        }

        ReleaseEntries(this.entries.Length);
    }

    private void ReleaseEntries(int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            if (this.entries[i].D3D12CommandList is not null)
            {
                _ = this.entries[i].D3D12CommandList->Release();

                this.entries[i].D3D12CommandList = null;
            }

            if (this.entries[i].D3D12CommandAllocator is not null)
            {
                _ = this.entries[i].D3D12CommandAllocator->Release();

                this.entries[i].D3D12CommandAllocator = null;
            }
        }
    }
}
