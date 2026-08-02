using System;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace ComputeWeave.Tests;

public unsafe partial class InteropServicesTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceFromBuffer(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(128);

        Assert.AreEqual(0, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);

        NativeResourceReference reference = InteropServices.AcquireNativeResource(buffer, out _);

        try
        {
            Assert.IsTrue(reference.IsValid);
            Assert.AreEqual(1, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);

            using ComPtr<ID3D12Resource> d3D12Resource = default;

            reference.QueryInterface(Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

            Assert.IsTrue(d3D12Resource.Get() != null);
            Assert.AreEqual(d3D12Resource.Get()->GetDesc().Dimension, D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_BUFFER);
        }
        finally
        {
            reference.Dispose();
        }

        Assert.IsFalse(reference.IsValid);
        Assert.AreEqual(0, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceIgnoresRedundantDisposal(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(128);

        NativeResourceReference reference = InteropServices.AcquireNativeResource(buffer, out _);

        reference.Dispose();
        reference.Dispose();

        Assert.AreEqual(0, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceDefersTheReleaseOfItsResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ulong before = GetOwnedBytes(graphicsDevice);

        ReadWriteBuffer<float> buffer = graphicsDevice.AllocateReadWriteBuffer<float>(1 << 16);
        NativeResourceReference reference = InteropServices.AcquireNativeResource(buffer, out _);

        ulong allocated = GetOwnedBytes(graphicsDevice);

        Assert.IsTrue(allocated > before);

        buffer.Dispose();

        Assert.AreEqual(allocated, GetOwnedBytes(graphicsDevice));
        Assert.AreEqual(1, graphicsDevice.GetMemoryStatistics().NativeReferencedGenerationCount);

        using ComPtr<ID3D12Resource> d3D12Resource = default;

        reference.QueryInterface(Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        Assert.AreEqual(d3D12Resource.Get()->GetDesc().Width, (1ul << 16) * sizeof(float));

        reference.Dispose();

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
        Assert.AreEqual(0, graphicsDevice.GetMemoryStatistics().NativeReferencedGenerationCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceDefersTheReleaseOfATransferResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ulong before = GetOwnedBytes(graphicsDevice);

        ReadBackBuffer<float> buffer = graphicsDevice.AllocateReadBackBuffer<float>(1 << 16);
        NativeResourceReference reference = InteropServices.AcquireNativeResource(buffer, out NativeResourceSynchronization synchronization);

        ulong allocated = GetOwnedBytes(graphicsDevice);

        Assert.IsTrue(allocated > before);
        Assert.IsTrue(synchronization.LastWrite.IsNone);

        buffer.Dispose();

        Assert.AreEqual(allocated, GetOwnedBytes(graphicsDevice));

        using ComPtr<ID3D12Resource> d3D12Resource = default;

        reference.QueryInterface(Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        Assert.AreEqual(d3D12Resource.Get()->GetDesc().Width, (1ul << 16) * sizeof(float));

        reference.Dispose();

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceFromTransferTexture(Device device)
    {
        using UploadTexture2D<float> texture = device.Get().AllocateUploadTexture2D<float>(32, 32);

        using NativeResourceReference reference = InteropServices.AcquireNativeResource(texture, out _);

        using ComPtr<ID3D12Resource> d3D12Resource = default;

        reference.QueryInterface(Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        Assert.AreEqual(d3D12Resource.Get()->GetDesc().Dimension, D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_BUFFER);
        Assert.AreEqual(1, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeDeviceKeepsTheNativeObjectValid(Device device)
    {
        using NativeDeviceReference reference = InteropServices.AcquireNativeDevice(device.Get());

        Assert.IsTrue(reference.IsValid);

        using ComPtr<ID3D12Device> d3D12Device = default;

        reference.QueryInterface(Windows.__uuidof<ID3D12Device>(), (void**)d3D12Device.GetAddressOf());

        Assert.IsTrue(d3D12Device.Get() != null);

        LUID luid = d3D12Device.Get()->GetAdapterLuid();

        Assert.IsTrue(*(ulong*)&luid != 0);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeDeviceIgnoresRedundantDisposal(Device device)
    {
        NativeDeviceReference reference = InteropServices.AcquireNativeDevice(device.Get());

        reference.Dispose();
        reference.Dispose();

        Assert.IsFalse(reference.IsValid);

        using ComPtr<ID3D12Device> d3D12Device = default;

        Assert.AreEqual(E.E_FAIL, reference.TryQueryInterface(Windows.__uuidof<ID3D12Device>(), (void**)d3D12Device.GetAddressOf()));
    }

    private static ulong GetOwnedBytes(GraphicsDevice device)
    {
        GraphicsMemoryStatistics statistics = device.GetMemoryStatistics();

        return statistics.Local.ComputeWeaveOwnedBytes + statistics.NonLocal.ComputeWeaveOwnedBytes;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceReportsTheCompletionOfSubmittedWork(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(128);

        using (ComputeContext context = device.Get().CreateComputeContext())
        {
            context.Clear(buffer);
            context.Submit();
        }

        using NativeResourceReference reference = InteropServices.AcquireNativeResource(
            buffer,
            out NativeResourceSynchronization synchronization,
            NativeResourceAcquisition.AfterPendingWork);

        Assert.IsFalse(synchronization.LastWrite.IsNone);
        Assert.AreEqual(ComputeQueueKind.Compute, synchronization.LastWrite.Queue);

        using ComPtr<ID3D12Fence> d3D12Fence = default;

        InteropServices.GetID3D12Fence(
            device.Get(),
            synchronization.LastWrite.Queue,
            Windows.__uuidof<ID3D12Fence>(),
            (void**)d3D12Fence.GetAddressOf());

        Assert.IsTrue(d3D12Fence.Get()->GetCompletedValue() >= synchronization.LastWrite.Value);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceDoesNotAllocateManagedMemory(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(128);

        for (int i = 0; i < 4; i++)
        {
            InteropServices.AcquireNativeResource(buffer, out _).Dispose();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long minimum = long.MaxValue;

        for (int i = 0; i < 16; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            InteropServices.AcquireNativeResource(buffer, out _).Dispose();

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.AreEqual(0, minimum);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquireNativeResourceFromTexture(Device device)
    {
        using ReadWriteTexture2D<float> texture = device.Get().AllocateReadWriteTexture2D<float>(32, 32);

        using NativeResourceReference reference = InteropServices.AcquireNativeResource(texture, out _);

        using ComPtr<ID3D12Resource> d3D12Resource = default;

        reference.QueryInterface(Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        Assert.AreEqual(d3D12Resource.Get()->GetDesc().Dimension, D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D);
        Assert.AreEqual(1, device.Get().GetMemoryStatistics().NativeReferencedGenerationCount);
    }
}
