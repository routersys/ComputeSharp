using System;
using ComputeSharp.Memory;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_MEMORY_POOL;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public partial class GraphicsMemoryTopologyTests
{
    [TestMethod]
    public void MapsEveryMemoryPoolOnUnifiedMemoryArchitectures()
    {
        Assert.IsTrue(GraphicsMemorySegments.TryMapMemoryPool(true, D3D12_MEMORY_POOL_L0, out MemoryPlacement placement));
        Assert.AreEqual(MemoryPlacement.Local, placement);

        Assert.IsFalse(GraphicsMemorySegments.TryMapMemoryPool(true, D3D12_MEMORY_POOL_L1, out _));
        Assert.IsFalse(GraphicsMemorySegments.TryMapMemoryPool(true, D3D12_MEMORY_POOL_UNKNOWN, out _));
    }

    [TestMethod]
    public void MapsEveryMemoryPoolOnDiscreteArchitectures()
    {
        Assert.IsTrue(GraphicsMemorySegments.TryMapMemoryPool(false, D3D12_MEMORY_POOL_L0, out MemoryPlacement local));
        Assert.AreEqual(MemoryPlacement.NonLocal, local);

        Assert.IsTrue(GraphicsMemorySegments.TryMapMemoryPool(false, D3D12_MEMORY_POOL_L1, out MemoryPlacement device));
        Assert.AreEqual(MemoryPlacement.Local, device);

        Assert.IsFalse(GraphicsMemorySegments.TryMapMemoryPool(false, D3D12_MEMORY_POOL_UNKNOWN, out _));
    }

    [TestMethod]
    public void ReportsActiveSegmentsPerTopology()
    {
        Assert.IsTrue(GraphicsMemorySegments.IsSegmentActive(true, MemoryPlacement.Local));
        Assert.IsFalse(GraphicsMemorySegments.IsSegmentActive(true, MemoryPlacement.NonLocal));

        Assert.IsTrue(GraphicsMemorySegments.IsSegmentActive(false, MemoryPlacement.Local));
        Assert.IsTrue(GraphicsMemorySegments.IsSegmentActive(false, MemoryPlacement.NonLocal));
    }

    [TestMethod]
    public void RejectsInvalidAllocationInfo()
    {
        Assert.AreEqual(GraphicsAllocationInfoStatus.ApiError, GraphicsAllocationInfo.Validate(ulong.MaxValue, 65536));
        Assert.AreEqual(GraphicsAllocationInfoStatus.UnsupportedPlan, GraphicsAllocationInfo.Validate(1024, 0));
        Assert.AreEqual(GraphicsAllocationInfoStatus.UnsupportedPlan, GraphicsAllocationInfo.Validate(1024, 128));
        Assert.AreEqual(GraphicsAllocationInfoStatus.UnsupportedPlan, GraphicsAllocationInfo.Validate(1024, 131072));
    }

    [TestMethod]
    public void AcceptsEverySupportedAlignment()
    {
        Assert.AreEqual(GraphicsAllocationInfoStatus.Valid, GraphicsAllocationInfo.Validate(1024, 4096));
        Assert.AreEqual(GraphicsAllocationInfoStatus.Valid, GraphicsAllocationInfo.Validate(1024, 65536));
        Assert.AreEqual(GraphicsAllocationInfoStatus.Valid, GraphicsAllocationInfo.Validate(1024, 4194304));
    }

    [TestMethod]
    public void SumsEveryGroupMemberIndividually()
    {
        Assert.AreEqual(
            GraphicsAllocationInfoStatus.Valid,
            GraphicsAllocationInfo.TrySum([1024, 2048, 4096], [4096, 65536, 4194304], out ulong total));

        Assert.AreEqual(7168ul, total);
    }

    [TestMethod]
    public void RejectsGroupsWithAnyInvalidMember()
    {
        Assert.AreEqual(
            GraphicsAllocationInfoStatus.ApiError,
            GraphicsAllocationInfo.TrySum([1024, ulong.MaxValue], [4096, 65536], out ulong apiErrorTotal));

        Assert.AreEqual(0ul, apiErrorTotal);

        Assert.AreEqual(
            GraphicsAllocationInfoStatus.UnsupportedPlan,
            GraphicsAllocationInfo.TrySum([1024, 2048], [4096, 1], out ulong unsupportedTotal));

        Assert.AreEqual(0ul, unsupportedTotal);

        Assert.AreEqual(
            GraphicsAllocationInfoStatus.UnsupportedPlan,
            GraphicsAllocationInfo.TrySum([ulong.MaxValue - 1, 4096], [4096, 4096], out ulong overflowTotal));

        Assert.AreEqual(0ul, overflowTotal);

        _ = Assert.ThrowsExactly<ArgumentException>(() => GraphicsAllocationInfo.TrySum([1024], [4096, 4096], out _));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsUmaSeparatelyFromCacheCoherentUma(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        if (graphicsDevice.IsCacheCoherentUMA)
        {
            Assert.IsTrue(graphicsDevice.IsUma);
        }

        Assert.IsTrue(GraphicsMemorySegments.TryMapMemoryPool(graphicsDevice.IsUma, D3D12_MEMORY_POOL_L0, out MemoryPlacement placement));
        Assert.AreEqual(graphicsDevice.IsUma ? MemoryPlacement.Local : MemoryPlacement.NonLocal, placement);
        Assert.AreEqual(!graphicsDevice.IsUma, GraphicsMemorySegments.IsSegmentActive(graphicsDevice.IsUma, MemoryPlacement.NonLocal));
    }
}
