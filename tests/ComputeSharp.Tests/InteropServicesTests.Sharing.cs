using System.Runtime.InteropServices;
using System.Threading;
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
}
