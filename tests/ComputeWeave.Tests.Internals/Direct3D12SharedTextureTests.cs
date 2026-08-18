using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.Windows;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the shipped Direct3D12 queue provider against real shared textures.
/// </summary>
[TestClass]
public unsafe class Direct3D12SharedTextureTests
{
    private sealed class Fixture : IDisposable
    {
        private Fixture(GraphicsDevice device, Direct3D12ExternalQueue queue, ComputeExternalQueueScheduler scheduler)
        {
            Device = device;
            Queue = queue;
            Scheduler = scheduler;
        }

        public GraphicsDevice Device { get; }

        public Direct3D12ExternalQueue Queue { get; }

        public ComputeExternalQueueScheduler Scheduler { get; }

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public ComputeInteropResourceSetRuntime Resources { get; private set; } = null!;

        public SharedTextureSlot<Bgra32, Float4, ExternalDirect3D12TextureView> Slot { get; } = new();

        public static Fixture Create(Device device, ComputeSharedTextureInitialOwner initialOwner)
        {
            GraphicsDevice graphicsDevice = device.Get();
            Direct3D12ExternalQueue queue = Direct3D12ExternalQueue.Create(graphicsDevice.Luid.ToInt64());
            Fixture fixture = new(graphicsDevice, queue, ComputeExternalQueueScheduler.Create());

            ComputeExternalDirect3D12Provider provider = new(
                (nint)queue.D3D12Device,
                (nint)queue.D3D12Queue,
                fixture.Scheduler);

            fixture.Domain = graphicsDevice.RegisterExternalDomain(provider);
            fixture.Resources = ComputeInteropResourceSetRuntime.Create(
                graphicsDevice,
                fixture.Domain,
                InteropResourceSetRegistrationTests.ResourceSetDescriptor(1, initialOwner),
                [fixture.Slot]);

            return fixture;
        }

        public void Dispose()
        {
            Slot.Dispose();
            Resources.Dispose();
            Domain.Dispose();
            Scheduler.Dispose();
            Queue.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OpensTheSharedTextureOnARealDirect3D12Device(Device device)
    {
        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(64, 32, out bool changed));
        Assert.IsTrue(changed);

        using BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation();

        Assert.IsTrue(borrow.IsValid);
        Assert.AreNotEqual(0, borrow.DangerousGetView().Resource);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Direct3D12WritesAreVisibleThroughTheComputeTexture(Device device)
    {
        const int Width = 64;
        const int Height = 32;

        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(Width, Height, out _));

        ReadWriteTexture2D<Bgra32, Float4> texture = fixture.Slot.GetComputeBinding().Resource!;

        Assert.IsNotNull(texture);

        uint[] source = new uint[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                source[(y * Width) + x] = unchecked((uint)((0xFF << 24) | ((x & 0xFF) << 16) | ((y & 0xFF) << 8) | ((x ^ y) & 0xFF)));
            }
        }

        using (BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation())
        {
            Assert.IsTrue(borrow.IsValid);

            fixture.Queue.Write(borrow.DangerousGetView().Resource, source, Width, Height);
        }

        Bgra32[,] readBack = new Bgra32[Height, Width];

        texture.CopyTo(readBack);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Assert.AreEqual(
                    source[(y * Width) + x],
                    readBack[y, x].PackedValue,
                    $"The pixel at ({x}, {y}) differs between the Direct3D 12 view and the compute texture.");
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TheDirect3D12ViewAliasesTheComputeTextureWithoutACopy(Device device)
    {
        const int Width = 32;
        const int Height = 16;

        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(Width, Height, out _));

        ReadWriteTexture2D<Bgra32, Float4> texture = fixture.Slot.GetComputeBinding().Resource!;

        uint[] first = new uint[Width * Height];
        uint[] second = new uint[Width * Height];

        Array.Fill(first, 0xFF102030u);
        Array.Fill(second, 0xFF4050C0u);

        using (BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation())
        {
            fixture.Queue.Write(borrow.DangerousGetView().Resource, first, Width, Height);
        }

        Bgra32[,] afterFirst = new Bgra32[Height, Width];

        texture.CopyTo(afterFirst);

        using (BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation())
        {
            fixture.Queue.Write(borrow.DangerousGetView().Resource, second, Width, Height);
        }

        Bgra32[,] afterSecond = new Bgra32[Height, Width];

        texture.CopyTo(afterSecond);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Assert.AreEqual(0xFF102030u, afterFirst[y, x].PackedValue);
                Assert.AreEqual(0xFF4050C0u, afterSecond[y, x].PackedValue);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ResizingPublishesANewDirect3D12View(Device device)
    {
        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 16, out _));

        nint first;

        using (BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation())
        {
            // 旧世代の資源を生かしたまま比較するため、所有参照を取って住所の再利用を防ぐ。
            first = borrow.DangerousGetView().AddRefResource();
        }

        try
        {
            Assert.IsTrue(fixture.Slot.TryEnsure(64, 48, out bool changed));
            Assert.IsTrue(changed);

            using BorrowedExternalTextureView<ExternalDirect3D12TextureView> borrow = fixture.Slot.BeginExternalOperation();

            Assert.AreNotEqual(0, borrow.DangerousGetView().Resource);
            Assert.AreNotEqual(first, borrow.DangerousGetView().Resource);
        }
        finally
        {
            _ = ((IUnknown*)first)->Release();
        }
    }
}
