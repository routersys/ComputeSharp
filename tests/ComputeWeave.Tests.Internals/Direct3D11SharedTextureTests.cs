using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class Direct3D11SharedTextureTests
{
    private sealed class Fixture : IDisposable
    {
        private Fixture(GraphicsDevice device, Direct3D11ImmediateContext context)
        {
            Device = device;
            Context = context;
        }

        public GraphicsDevice Device { get; }

        public Direct3D11ImmediateContext Context { get; }

        public Direct3D11InteropProvider Provider { get; private set; } = null!;

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public ComputeInteropResourceSetRuntime Resources { get; private set; } = null!;

        public SharedTextureSlot<Bgra32, Float4, Direct3D11ExternalView> Slot { get; } = new();

        public static Fixture Create(Device device, ComputeSharedTextureInitialOwner initialOwner)
        {
            GraphicsDevice graphicsDevice = device.Get();
            Direct3D11ImmediateContext context = Direct3D11ImmediateContext.Create(graphicsDevice.Luid.ToInt64());
            Fixture fixture = new(graphicsDevice, context);

            fixture.Provider = new Direct3D11InteropProvider(context);
            fixture.Domain = graphicsDevice.RegisterExternalDomain(fixture.Provider);
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
            Context.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OpensTheSharedTextureOnARealDirect3D11Device(Device device)
    {
        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(64, 32, out bool changed));
        Assert.IsTrue(changed);

        Assert.AreEqual(1, fixture.Provider.OpenSharedTextureCount);
        Assert.IsNotNull(fixture.Provider.LastOpenedView);
        Assert.AreEqual(64, fixture.Provider.LastOpenedView!.Width);
        Assert.AreEqual(32, fixture.Provider.LastOpenedView.Height);
        Assert.IsTrue(fixture.Provider.OpenedSharedFence);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ComputeWritesAreVisibleThroughTheDirect3D11View(Device device)
    {
        const int Width = 64;
        const int Height = 32;

        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(Width, Height, out _));

        ReadWriteTexture2D<Bgra32, Float4> texture = fixture.Slot.GetComputeBinding().Resource!;

        Assert.IsNotNull(texture);

        Bgra32[,] source = new Bgra32[Height, Width];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                source[y, x].PackedValue = unchecked((uint)((0xFF << 24) | ((x & 0xFF) << 16) | ((y & 0xFF) << 8) | ((x ^ y) & 0xFF)));
            }
        }

        texture.CopyFrom(source);

        uint[] readBack;

        using (BorrowedExternalTextureView<Direct3D11ExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            Assert.IsTrue(borrow.IsValid);

            Direct3D11ExternalView view = borrow.DangerousGetView();

            readBack = fixture.Context.ReadBack(view.D3D11Texture, Width, Height);
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Assert.AreEqual(
                    source[y, x].PackedValue,
                    readBack[(y * Width) + x],
                    $"The pixel at ({x}, {y}) differs between the compute texture and the Direct3D 11 view.");
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TheDirect3D11ViewAliasesTheComputeTextureWithoutACopy(Device device)
    {
        const int Width = 32;
        const int Height = 16;

        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(Width, Height, out _));

        ReadWriteTexture2D<Bgra32, Float4> texture = fixture.Slot.GetComputeBinding().Resource!;

        Bgra32[,] first = new Bgra32[Height, Width];
        Bgra32[,] second = new Bgra32[Height, Width];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                first[y, x].PackedValue = 0xFF102030u;
                second[y, x].PackedValue = 0xFF4050C0u;
            }
        }

        texture.CopyFrom(first);

        uint[] afterFirst;
        uint[] afterSecond;

        using (BorrowedExternalTextureView<Direct3D11ExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            afterFirst = fixture.Context.ReadBack(borrow.DangerousGetView().D3D11Texture, Width, Height);
        }

        texture.CopyFrom(second);

        using (BorrowedExternalTextureView<Direct3D11ExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            afterSecond = fixture.Context.ReadBack(borrow.DangerousGetView().D3D11Texture, Width, Height);
        }

        Assert.AreEqual(0xFF102030u, afterFirst[0]);
        Assert.AreEqual(0xFF4050C0u, afterSecond[0]);

        for (int index = 0; index < afterFirst.Length; index++)
        {
            Assert.AreEqual(0xFF102030u, afterFirst[index]);
            Assert.AreEqual(0xFF4050C0u, afterSecond[index]);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ResizingPublishesANewDirect3D11View(Device device)
    {
        using Fixture fixture = Fixture.Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 16, out _));

        Direct3D11ExternalView first = fixture.Provider.LastOpenedView!;

        Assert.IsTrue(fixture.Slot.TryEnsure(64, 48, out bool changed));
        Assert.IsTrue(changed);
        Assert.AreEqual(2, fixture.Provider.OpenSharedTextureCount);

        Direct3D11ExternalView second = fixture.Provider.LastOpenedView!;

        Assert.AreNotSame(first, second);
        Assert.AreEqual(64, second.Width);
        Assert.AreEqual(48, second.Height);
    }
}
