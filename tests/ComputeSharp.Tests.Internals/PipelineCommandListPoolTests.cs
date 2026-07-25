using System;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            partition.Rent(null, out PipelineCommandListRental first);
            partition.Rent(null, out PipelineCommandListRental second);

            Assert.AreEqual(0, partition.AvailableCount);
            Assert.AreNotEqual(first.Index, second.Index);
            Assert.IsTrue(first.D3D12CommandList != second.D3D12CommandList);
            Assert.IsTrue(first.D3D12CommandAllocator != second.D3D12CommandAllocator);
            Assert.IsTrue(first.D3D12CommandList is not null);
            Assert.IsTrue(first.D3D12CommandAllocator is not null);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _));

            partition.Return(first.Index, isCommandListClosed: false);
            partition.Return(second.Index, isCommandListClosed: false);

            Assert.AreEqual(2, partition.AvailableCount);

            partition.Rent(null, out PipelineCommandListRental reused);

            Assert.AreEqual(first.Index, reused.Index);
            Assert.IsTrue(reused.D3D12CommandList == first.D3D12CommandList);

            partition.Return(reused.Index, isCommandListClosed: false);
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
            partition.Rent(null, out PipelineCommandListRental rental);

            _ = rental.D3D12CommandList->Close();

            partition.Return(rental.Index, isCommandListClosed: true);

            partition.Rent(null, out PipelineCommandListRental reused);

            _ = reused.D3D12CommandList->Close();

            partition.Return(reused.Index, isCommandListClosed: true);
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
            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => partition.Return(1, isCommandListClosed: false));
            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => partition.Return(-1, isCommandListClosed: false));
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(0, isCommandListClosed: false));

            partition.Rent(null, out PipelineCommandListRental rental);
            partition.Return(rental.Index, isCommandListClosed: false);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(rental.Index, isCommandListClosed: false));
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

        partition.Rent(null, out PipelineCommandListRental rental);

        Assert.IsTrue(partition.HasRentedEntries);

        partition.Return(rental.Index, isCommandListClosed: false);

        Assert.IsFalse(partition.HasRentedEntries);

        partition.Dispose();
        partition.Dispose();

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(0, isCommandListClosed: false));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PipelineCommandListPartition_SupportsEmptyReservation(Device device)
    {
        PipelineCommandListPartition partition = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE, 0);

        try
        {
            Assert.AreEqual(0, partition.Capacity);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _));
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

            first.Rent(null, out PipelineCommandListRental rental);

            Assert.AreEqual(0, first.AvailableCount);
            Assert.AreEqual(2, second.AvailableCount);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => first.Rent(null, out _));

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => pool.DestroyPartition(first));

            first.Return(rental.Index, isCommandListClosed: false);

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
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Rent(null, out _));
    }
}
