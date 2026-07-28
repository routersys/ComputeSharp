using System;
using System.Threading;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe class SharedTextureGenerationTests
{
    private sealed class Fixture(GraphicsDevice device) : IDisposable
    {
        public FakeInteropScheduler Scheduler { get; } = new();

        public FakeInteropProvider Provider { get; private set; } = null!;

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public ComputeInteropResourceSetRuntime Resources { get; private set; } = null!;

        public SharedTextureSlot<Bgra32, Float4, FakeExternalView> Slot { get; } = new();

        public Fixture Register()
        {
            Provider = new FakeInteropProvider(device, Scheduler);
            Domain = device.RegisterExternalDomain(Provider);
            Resources = ComputeInteropResourceSetRuntime.Create(
                device,
                Domain,
                InteropResourceSetRegistrationTests.ResourceSetDescriptor(1),
                [Slot]);

            return this;
        }

        public void Dispose()
        {
            Resources.Dispose();
            Domain.Dispose();
            Scheduler.Dispose();
        }
    }

    private static Fixture Create(Device device)
    {
        return new Fixture(device.Get()).Register();
    }

    private static void WaitForExternalRelease(Fixture fixture, FakeExternalView view)
    {
        for (int i = 0; i < 5000 && view.DisposeCount == 0; i++)
        {
            ((IComputeSharedSlot)fixture.Slot).RunMaintenance();

            if (view.DisposeCount == 0)
            {
                Thread.Sleep(1);
            }
        }

        Assert.AreEqual(1, view.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesASharedTextureGenerationWithItsExternalView(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Slot.TryEnsure(64, 32, out bool changed));
        Assert.IsTrue(changed);

        Assert.IsTrue(fixture.Slot.IsAllocated);
        Assert.AreEqual(64, fixture.Slot.Width);
        Assert.AreEqual(32, fixture.Slot.Height);

        Assert.AreEqual(1, fixture.Provider.OpenSharedTextureCount);
        Assert.IsTrue(fixture.Provider.WasReservedWhileOpeningTexture);
        Assert.IsFalse(fixture.Scheduler.IsReserved);

        Assert.AreEqual(64, fixture.Provider.ObservedTextureDescriptor.Width);
        Assert.AreEqual(32, fixture.Provider.ObservedTextureDescriptor.Height);
        Assert.AreEqual(ExternalTextureFormat.Bgra8Unorm, fixture.Provider.ObservedTextureDescriptor.Format);
        Assert.AreEqual(ExternalTextureUsage.RenderTarget, fixture.Provider.ObservedTextureDescriptor.ExternalUsage);
        Assert.AreEqual(ComputeAlphaMode.Premultiplied, fixture.Provider.ObservedTextureDescriptor.AlphaMode);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClosesTheTemporarySharedHandleAfterOpeningTheTexture(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        Assert.AreNotEqual(0, fixture.Provider.ObservedTextureHandle);
        Assert.IsFalse(Windows.CloseHandle(new HANDLE((void*)fixture.Provider.ObservedTextureHandle)));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnIdenticalPlanKeepsThePublishedGeneration(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 32, out bool changed));
        Assert.IsTrue(changed);

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 32, out changed));
        Assert.IsFalse(changed);

        Assert.AreEqual(1, fixture.Provider.OpenSharedTextureCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void BindsThePublishedGenerationToTheComputeQueue(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Slot.TryEnsure(8, 8, out _));

        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> binding = fixture.Slot.GetComputeBinding();

        Assert.IsTrue(binding.IsValid);

        ReadWriteTexture2D<Bgra32, Float4> texture = binding.Resource!;

        Assert.AreEqual(8, texture.Width);
        Assert.AreEqual(8, texture.Height);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AReplacementReleasesTheExternalViewOfTheRetiredGeneration(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView first = fixture.Provider.LastOpenedView!;

        Assert.AreEqual(0, first.DisposeCount);

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 16, out bool changed));
        Assert.IsTrue(changed);

        FakeExternalView second = fixture.Provider.LastOpenedView!;

        Assert.AreNotSame(first, second);
        Assert.AreEqual(2, fixture.Provider.OpenSharedTextureCount);
        Assert.AreEqual(0, second.DisposeCount);
        Assert.AreEqual(32, fixture.Slot.Width);

        WaitForExternalRelease(fixture, first);

        Assert.AreEqual(0, second.DisposeCount);
        Assert.IsFalse(fixture.Scheduler.IsReserved);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposingTheSlotReleasesItsExternalView(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

            FakeExternalView view = fixture.Provider.LastOpenedView!;

            fixture.Slot.Dispose();

            Assert.IsFalse(fixture.Slot.IsAllocated);

            fixture.Slot.WaitForDisposal();

            Assert.AreEqual(1, view.DisposeCount);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposingTheResourceSetReleasesEveryExternalView(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

            FakeExternalView view = fixture.Provider.LastOpenedView!;

            fixture.Resources.Dispose();

            Assert.IsTrue(fixture.Resources.IsDisposeRequested);

            fixture.Resources.WaitForDisposal();

            Assert.AreEqual(1, view.DisposeCount);

            fixture.Domain.Dispose();

            Assert.IsTrue(fixture.Domain.IsDisposed);
            Assert.AreEqual(1, fixture.Provider.DisposeCount);
        }
        finally
        {
            fixture.Scheduler.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAnEnsureOnADisposedResourceSet(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            fixture.Resources.Dispose();

            _ = Assert.ThrowsException<InvalidOperationException>(() => _ = fixture.Slot.TryEnsure(16, 16, out _));
        }
        finally
        {
            fixture.Domain.Dispose();
            fixture.Scheduler.Dispose();
        }
    }
}
