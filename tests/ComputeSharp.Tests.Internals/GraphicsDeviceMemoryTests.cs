using System;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Memory;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class GraphicsDeviceMemoryTests
{
    private static ulong GetExpectedAllocationBytes(GraphicsDevice device, ulong sizeInBytes)
    {
        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetCommittedResourceDescription(
            ResourceType.ReadWrite,
            sizeInBytes,
            device.IsCacheCoherentUMA);

        return device.D3D12Device->GetResourceAllocationInfo(in description).SizeInBytes;
    }

    private static GraphicsMemoryStatistics GetStableStatistics(GraphicsDevice device)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        return device.GetMemoryStatistics();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AccountsEveryCommittedResourceExactly(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        const int length = 1024;

        ulong expectedBytes = GetExpectedAllocationBytes(graphicsDevice, length * sizeof(float));

        Assert.AreNotEqual(0ul, expectedBytes);

        GraphicsMemoryStatistics before = GetStableStatistics(graphicsDevice);

        using (ReadWriteBuffer<float> buffer = graphicsDevice.AllocateReadWriteBuffer<float>(length))
        {
            GraphicsMemoryStatistics during = graphicsDevice.GetMemoryStatistics();

            Assert.AreEqual(before.Local.ComputeSharpOwnedBytes + expectedBytes, during.Local.ComputeSharpOwnedBytes);
            Assert.AreEqual(0ul, during.Local.ReservationBytes);
            Assert.AreEqual(0ul, during.NonLocal.ReservationBytes);
        }

        GraphicsMemoryStatistics after = graphicsDevice.GetMemoryStatistics();

        Assert.AreEqual(before.Local.ComputeSharpOwnedBytes, after.Local.ComputeSharpOwnedBytes);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AccountsTransferResourcesInTheSegmentTheyAreAllocatedFrom(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        GraphicsMemoryStatistics before = GetStableStatistics(graphicsDevice);

        using (UploadBuffer<float> buffer = graphicsDevice.AllocateUploadBuffer<float>(1024))
        {
            GraphicsMemoryStatistics during = graphicsDevice.GetMemoryStatistics();

            if (graphicsDevice.IsUma)
            {
                Assert.IsTrue(during.Local.ComputeSharpOwnedBytes > before.Local.ComputeSharpOwnedBytes);
                Assert.AreEqual(0ul, during.NonLocal.ComputeSharpOwnedBytes);
            }
            else
            {
                Assert.IsTrue(during.NonLocal.ComputeSharpOwnedBytes > before.NonLocal.ComputeSharpOwnedBytes);
                Assert.AreEqual(before.Local.ComputeSharpOwnedBytes, during.Local.ComputeSharpOwnedBytes);
            }
        }

        GraphicsMemoryStatistics after = graphicsDevice.GetMemoryStatistics();

        Assert.AreEqual(before.Local.ComputeSharpOwnedBytes, after.Local.ComputeSharpOwnedBytes);
        Assert.AreEqual(before.NonLocal.ComputeSharpOwnedBytes, after.NonLocal.ComputeSharpOwnedBytes);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsInactiveSegmentsWithoutAnyByteValue(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        GraphicsMemoryStatistics statistics = graphicsDevice.GetMemoryStatistics();

        Assert.AreEqual(GraphicsMemorySegment.Local, statistics.Local.Segment);
        Assert.AreEqual(GraphicsMemorySegment.NonLocal, statistics.NonLocal.Segment);
        Assert.AreEqual(MemoryBudgetStatus.Valid, statistics.Local.Status);
        Assert.AreNotEqual(0ul, statistics.Local.BudgetBytes);

        if (graphicsDevice.IsUma)
        {
            Assert.AreEqual(MemoryBudgetStatus.Unsupported, statistics.NonLocal.Status);
            Assert.AreEqual(0ul, statistics.NonLocal.BudgetBytes);
            Assert.AreEqual(0ul, statistics.NonLocal.CurrentProcessUsageBytes);
            Assert.AreEqual(0ul, statistics.NonLocal.ComputeSharpOwnedBytes);
            Assert.AreEqual(0ul, statistics.NonLocal.ReservationBytes);
            Assert.AreEqual(0ul, statistics.NonLocal.RetiredPendingBytes);
        }
        else
        {
            Assert.AreEqual(MemoryBudgetStatus.Valid, statistics.NonLocal.Status);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AdvancesTheObservationEpochAcrossMemoryOperations(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        ulong epoch = graphicsDevice.GetMemoryStatistics().Epoch;

        Assert.AreNotEqual(0ul, epoch);

        graphicsDevice.TrimMemory();

        Assert.IsTrue(graphicsDevice.GetMemoryStatistics().Epoch >= epoch);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsEveryAllocationBeyondTheExplicitHardLimit(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        try
        {
            graphicsDevice.SetMemoryPolicy(new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 0, NonLocalOwnedHardLimitBytes = 0 });

            _ = Assert.ThrowsExactly<GraphicsMemoryAllocationException>(() => _ = graphicsDevice.AllocateReadWriteBuffer<float>(1024));
        }
        finally
        {
            graphicsDevice.SetMemoryPolicy(default);
        }

        using ReadWriteBuffer<float> buffer = graphicsDevice.AllocateReadWriteBuffer<float>(1024);

        Assert.AreEqual(1024, buffer.Length);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsTheOwnedBytesOfExistingResourcesWhenTheLimitIsLowered(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using ReadWriteBuffer<float> buffer = graphicsDevice.AllocateReadWriteBuffer<float>(1024);

        ulong ownedBytes = graphicsDevice.GetMemoryStatistics().Local.ComputeSharpOwnedBytes;

        try
        {
            graphicsDevice.SetMemoryPolicy(new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 0 });

            Assert.AreEqual(ownedBytes, graphicsDevice.GetMemoryStatistics().Local.ComputeSharpOwnedBytes);
        }
        finally
        {
            graphicsDevice.SetMemoryPolicy(default);
        }
    }
}
