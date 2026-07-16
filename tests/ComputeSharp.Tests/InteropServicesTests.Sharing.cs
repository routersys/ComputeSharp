using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Interop;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace ComputeSharp.Tests;

public unsafe partial class InteropServicesTests
{
    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int CloseHandle(nint hObject);

    [CombinatorialTestMethod]
    [AllDevices]
    public void DescriptorHandleAllocator_DefaultReturnIsIgnoredAndDuplicateReturnThrows(Device device)
    {
        ID3D12DescriptorHandleAllocator allocator = new(device.Get().D3D12Device);

        try
        {
            allocator.Return(default);
            allocator.Rent(out ID3D12ResourceDescriptorHandles handles);
            allocator.Return(in handles);

            Assert.ThrowsExactly<InvalidOperationException>(() => allocator.Return(in handles));
        }
        finally
        {
            allocator.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedReadWriteTexture2D_ComputeShaderRoundTrip(Device device)
    {
        using ReadWriteTexture2D<float> texture = InteropServices.AllocateSharedReadWriteTexture2D<float>(device.Get(), 16, 16);

        device.Get().For(16, 16, new SharedTextureFillShader(texture));

        float[] result = new float[16 * 16];

        texture.CopyTo(result);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.AreEqual(x + y, result[(y * 16) + x]);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedReadWriteTexture2D_RoundTripThroughSharedHandle(Device device)
    {
        using ReadWriteTexture2D<float> source = InteropServices.AllocateSharedReadWriteTexture2D<float>(device.Get(), 16, 16);

        device.Get().For(16, 16, new SharedTextureFillShader(source));

        nint handle = InteropServices.CreateSharedHandle(source);

        Assert.AreNotEqual(0, handle);

        try
        {
            using ReadWriteTexture2D<float> opened = InteropServices.OpenSharedReadWriteTexture2D<float>(device.Get(), handle);

            Assert.AreEqual(16, opened.Width);
            Assert.AreEqual(16, opened.Height);

            float[] result = new float[16 * 16];

            opened.CopyTo(result);

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Assert.AreEqual(x + y, result[(y * 16) + x]);
                }
            }
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedNormalizedReadWriteTexture2D_RoundTripThroughSharedHandle(Device device)
    {
        using ReadWriteTexture2D<Bgra32, Float4> source = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device.Get(), 16, 16);
        using ComPtr<ID3D12Resource> d3D12Resource = default;

        InteropServices.GetID3D12Resource(source, Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        D3D12_RESOURCE_FLAGS expectedFlags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET |
                                             D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS |
                                             D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS;

        Assert.AreEqual(expectedFlags, d3D12Resource.Get()->GetDesc().Flags & expectedFlags);

        device.Get().For(16, 16, new SharedNormalizedTextureFillShader(source));

        nint handle = InteropServices.CreateSharedHandle(source);

        Assert.AreNotEqual(0, handle);

        try
        {
            using ReadWriteTexture2D<Bgra32, Float4> opened = InteropServices.OpenSharedReadWriteTexture2D<Bgra32, Float4>(device.Get(), handle);

            Bgra32[] result = new Bgra32[16 * 16];

            opened.CopyTo(result);

            foreach (Bgra32 pixel in result)
            {
                Assert.AreEqual(0xFF808080u, pixel.PackedValue);
            }
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedNormalizedReadWriteTexture2D_AllPixelFormats(Device device)
    {
        AssertSharedNormalizedTextureFlags<Bgra32, Float4>(device.Get());
        AssertSharedNormalizedTextureFlags<R16, float>(device.Get());
        AssertSharedNormalizedTextureFlags<R8, float>(device.Get());
        AssertSharedNormalizedTextureFlags<Rg16, Float2>(device.Get());
        AssertSharedNormalizedTextureFlags<Rg32, Float2>(device.Get());
        AssertSharedNormalizedTextureFlags<Rgba32, Float4>(device.Get());
        AssertSharedNormalizedTextureFlags<Rgba64, Float4>(device.Get());
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ComputeContext_SubmitDoesNotWaitForCompletion(Device device)
    {
        using ReadWriteTexture2D<Bgra32, Float4> texture = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device.Get(), 16, 16);
        using ComPtr<ID3D12Fence> d3D12Fence = default;

        nint handle = 0;

        InteropServices.CreateSharedFence(device.Get(), Windows.__uuidof<ID3D12Fence>(), (void**)d3D12Fence.GetAddressOf(), &handle);

        try
        {
            InteropServices.WaitForSharedFence(device.Get(), d3D12Fence.Get(), 1);

            Task submitTask = Task.Run(() =>
            {
                using ComputeContext context = device.Get().CreateComputeContext();

                context.For(16, 16, new SharedNormalizedTextureFillShader(texture));
                context.Submit();
                InteropServices.SignalSharedFence(device.Get(), d3D12Fence.Get(), 2);
            });

            bool completedWithoutFence;

            try
            {
                completedWithoutFence = submitTask.Wait(TimeSpan.FromSeconds(5));
            }
            finally
            {
                Assert.IsTrue(d3D12Fence.Get()->Signal(1).SUCCEEDED);
                submitTask.Wait();
            }

            Assert.IsTrue(completedWithoutFence);

            while (d3D12Fence.Get()->GetCompletedValue() < 2)
            {
                _ = Thread.Yield();
            }

            Bgra32[] result = new Bgra32[16 * 16];

            texture.CopyTo(result);

            Assert.AreEqual(0xFF808080u, result[0].PackedValue);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ComputeContext_SubmitDoesNotAllocateAfterWarmup(Device device)
    {
        using ReadWriteTexture2D<Bgra32, Float4> texture = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device.Get(), 16, 16);
        using ComPtr<ID3D12Fence> d3D12Fence = default;

        nint handle = 0;

        InteropServices.CreateSharedFence(device.Get(), Windows.__uuidof<ID3D12Fence>(), (void**)d3D12Fence.GetAddressOf(), &handle);

        try
        {
            ulong fenceValue = 0;

            for (int i = 0; i < 4; i++)
            {
                SubmitAndWait(device.Get(), texture, d3D12Fence.Get(), ref fenceValue);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 32; i++)
            {
                SubmitAndWait(device.Get(), texture, d3D12Fence.Get(), ref fenceValue);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ComputeContext_SubmitAppliesBackpressureAtPendingLimit(Device device)
    {
        using ReadWriteTexture2D<Bgra32, Float4> texture = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device.Get(), 16, 16);
        using ComPtr<ID3D12Fence> d3D12Fence = default;

        nint handle = 0;

        InteropServices.CreateSharedFence(device.Get(), Windows.__uuidof<ID3D12Fence>(), (void**)d3D12Fence.GetAddressOf(), &handle);

        try
        {
            InteropServices.WaitForSharedFence(device.Get(), d3D12Fence.Get(), 1);

            for (int i = 0; i < 16; i++)
            {
                using ComputeContext context = device.Get().CreateComputeContext();

                context.For(16, 16, new SharedNormalizedTextureFillShader(texture));
                context.Submit();
            }

            using ManualResetEventSlim started = new();
            Task overflowTask = Task.Run(() =>
            {
                using ComputeContext context = device.Get().CreateComputeContext();

                context.For(16, 16, new SharedNormalizedTextureFillShader(texture));
                started.Set();
                context.Submit();
                InteropServices.SignalSharedFence(device.Get(), d3D12Fence.Get(), 2);
            });

            started.Wait();

            bool completedBeforeRelease;

            try
            {
                completedBeforeRelease = overflowTask.Wait(TimeSpan.FromMilliseconds(100));
            }
            finally
            {
                Assert.IsTrue(d3D12Fence.Get()->Signal(1).SUCCEEDED);
                Assert.IsTrue(overflowTask.Wait(TimeSpan.FromSeconds(5)));
            }

            Assert.IsFalse(completedBeforeRelease);

            while (d3D12Fence.Get()->GetCompletedValue() < 2)
            {
                _ = Thread.Yield();
            }

            Bgra32[] result = new Bgra32[16 * 16];

            texture.CopyTo(result);

            Assert.AreEqual(0xFF808080u, result[0].PackedValue);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedReadOnlyTexture2D_AllocateAndExportHandle(Device device)
    {
        using ReadOnlyTexture2D<float> texture = InteropServices.AllocateSharedReadOnlyTexture2D<float>(device.Get(), 16, 16);

        Assert.AreEqual(16, texture.Width);
        Assert.AreEqual(16, texture.Height);

        nint handle = InteropServices.CreateSharedHandle(texture);

        Assert.AreNotEqual(0, handle);

        _ = CloseHandle(handle);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SharedFence_SignalReachesValueOnComputeQueue(Device device)
    {
        using ComPtr<ID3D12Fence> d3D12Fence = default;

        nint handle = 0;

        InteropServices.CreateSharedFence(device.Get(), Windows.__uuidof<ID3D12Fence>(), (void**)d3D12Fence.GetAddressOf(), &handle);

        Assert.IsTrue(d3D12Fence.Get() != null);
        Assert.AreNotEqual(0, handle);

        try
        {
            Assert.AreEqual(0u, d3D12Fence.Get()->GetCompletedValue());

            InteropServices.SignalSharedFence(device.Get(), d3D12Fence.Get(), 7);

            int spin = 0;

            while (d3D12Fence.Get()->GetCompletedValue() < 7 && spin++ < 10000)
            {
                Thread.Sleep(1);
            }

            Assert.AreEqual(7u, d3D12Fence.Get()->GetCompletedValue());
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SharedTextureFillShader : IComputeShader
    {
        public readonly ReadWriteTexture2D<float> texture;

        public void Execute()
        {
            this.texture[ThreadIds.XY] = ThreadIds.X + ThreadIds.Y;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SharedNormalizedTextureFillShader : IComputeShader
    {
        public readonly ReadWriteTexture2D<Bgra32, Float4> texture;

        public void Execute()
        {
            this.texture[ThreadIds.XY] = new Float4(0.5f, 0.5f, 0.5f, 1f);
        }
    }

    private static void SubmitAndWait(GraphicsDevice device, ReadWriteTexture2D<Bgra32, Float4> texture, ID3D12Fence* d3D12Fence, ref ulong fenceValue)
    {
        using ComputeContext context = device.CreateComputeContext();

        context.For(16, 16, new SharedNormalizedTextureFillShader(texture));
        context.Submit();

        InteropServices.SignalSharedFence(device, d3D12Fence, ++fenceValue);

        while (d3D12Fence->GetCompletedValue() < fenceValue)
        {
            _ = Thread.Yield();
        }
    }

    private static void AssertSharedNormalizedTextureFlags<T, TPixel>(GraphicsDevice device)
        where T : unmanaged, IPixel<T, TPixel>
        where TPixel : unmanaged
    {
        using ReadWriteTexture2D<T, TPixel> texture = InteropServices.AllocateSharedReadWriteTexture2D<T, TPixel>(device, 1, 1);
        using ComPtr<ID3D12Resource> d3D12Resource = default;

        InteropServices.GetID3D12Resource(texture, Windows.__uuidof<ID3D12Resource>(), (void**)d3D12Resource.GetAddressOf());

        D3D12_RESOURCE_FLAGS expectedFlags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET |
                                             D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS |
                                             D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS;

        Assert.AreEqual(expectedFlags, d3D12Resource.Get()->GetDesc().Flags & expectedFlags);
    }
}
