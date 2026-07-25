using System;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class PipelineCommandListPoolTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_ReservesEveryEntryUpFront(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 4);

        try
        {
            Assert.AreEqual(4, partition.Capacity);
            Assert.AreEqual(4, partition.AvailableCount);
        }
        finally
        {
            partition.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_RentsAndReturnsEveryReservedEntry(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 2);

        try
        {
            partition.Rent(null, out ID3D12GraphicsCommandList* firstList, out ID3D12CommandAllocator* firstAllocator);
            partition.Rent(null, out ID3D12GraphicsCommandList* secondList, out ID3D12CommandAllocator* secondAllocator);

            Assert.AreEqual(0, partition.AvailableCount);
            Assert.IsTrue(firstList != secondList);
            Assert.IsTrue(firstAllocator != secondAllocator);
            Assert.IsTrue(firstList is not null);
            Assert.IsTrue(firstAllocator is not null);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _, out _));

            partition.Return(firstList, isCommandListClosed: false);
            partition.Return(secondList, isCommandListClosed: false);

            Assert.AreEqual(2, partition.AvailableCount);

            partition.Rent(null, out ID3D12GraphicsCommandList* reusedList, out _);

            Assert.IsTrue(reusedList == firstList);

            partition.Return(reusedList, isCommandListClosed: false);
        }
        finally
        {
            partition.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_RentedListIsRecordableAndClosable(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 1);

        try
        {
            partition.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);

            _ = d3D12CommandList->Close();

            partition.Return(d3D12CommandList, isCommandListClosed: true);

            partition.Rent(null, out ID3D12GraphicsCommandList* reusedList, out _);

            _ = reusedList->Close();

            partition.Return(reusedList, isCommandListClosed: true);
        }
        finally
        {
            partition.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_RejectsInvalidReturn(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 1);

        try
        {
            _ = Assert.ThrowsExactly<ArgumentException>(() => partition.Return(null, isCommandListClosed: false));

            partition.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);
            partition.Return(d3D12CommandList, isCommandListClosed: false);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(d3D12CommandList, isCommandListClosed: false));
        }
        finally
        {
            partition.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_ReportsRentedEntriesAndRejectsUseAfterDisposal(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 1);

        partition.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);

        Assert.IsTrue(partition.HasRentedEntries);

        partition.Return(d3D12CommandList, isCommandListClosed: false);

        Assert.IsFalse(partition.HasRentedEntries);

        partition.Dispose();
        partition.Dispose();

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _, out _));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(d3D12CommandList, isCommandListClosed: false));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_SupportsEmptyReservation(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 0);

        try
        {
            Assert.AreEqual(0, partition.Capacity);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _, out _));
        }
        finally
        {
            partition.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPool_KeepsPartitionsIsolated(Device device)
    {
        PipelineCommandListPool pool = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            PipelineCommandListPartition first = pool.CreatePartition(1);
            PipelineCommandListPartition second = pool.CreatePartition(2);

            Assert.AreEqual(2, pool.PartitionCount);

            first.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);

            Assert.AreEqual(0, first.AvailableCount);
            Assert.AreEqual(2, second.AvailableCount);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => first.Rent(null, out _, out _));
            _ = Assert.ThrowsExactly<ArgumentException>(() => second.Return(d3D12CommandList, isCommandListClosed: false));
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => pool.DestroyPartition(first));

            first.Return(d3D12CommandList, isCommandListClosed: false);

            pool.DestroyPartition(first);

            Assert.AreEqual(1, pool.PartitionCount);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => pool.DestroyPartition(first));
        }
        finally
        {
            pool.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPool_RejectsPartitionsAfterDisposal(Device device)
    {
        PipelineCommandListPool pool = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        PipelineCommandListPartition partition = pool.CreatePartition(1);

        Assert.AreEqual(1, partition.Capacity);

        pool.Dispose();
        pool.Dispose();

        Assert.AreEqual(0, pool.PartitionCount);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => pool.CreatePartition(1));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _, out _));
    }
}
