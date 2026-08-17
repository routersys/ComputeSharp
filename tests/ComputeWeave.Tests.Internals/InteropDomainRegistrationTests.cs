using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe partial class InteropDomainRegistrationTests
{
    private const ExternalInteropCapabilities RequiredCapabilities =
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering |
        ExternalInteropCapabilities.PersistentExternalViewOrdering;

    [CombinatorialTestMethod]
    [AllDevices]
    public void RegistersADomainAndInitializesItsProvider(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreSame(graphicsDevice, domain.Device);
        Assert.AreNotEqual(0ul, domain.Id.Value);
        Assert.AreEqual(RequiredCapabilities, domain.Capabilities);
        Assert.IsFalse(domain.IsDisposeRequested);
        Assert.IsFalse(domain.IsDisposed);

        Assert.AreEqual(1, provider.InitializeCount);
        Assert.IsTrue(provider.OpenedSharedFence);
        Assert.AreEqual(0, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClosesTheTemporarySharedFenceHandleAfterTheRegistration(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreNotEqual(0, provider.ObservedFenceHandle);
        Assert.IsFalse(Windows.CloseHandle(new HANDLE((void*)provider.ObservedFenceHandle)));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AssignsADistinctIdentifierToEveryDomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain first = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));
        using ComputeInteropDomain second = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        Assert.AreNotEqual(first.Id.Value, second.Id.Value);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAProviderBoundToAnotherAdapter(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler)
        {
            AdapterIdentity = new ExternalAdapterIdentity(graphicsDevice.Luid.ToInt64() ^ 1)
        };

        _ = Assert.ThrowsException<ArgumentException>(() => graphicsDevice.RegisterExternalDomain(provider));

        Assert.AreEqual(0, provider.InitializeCount);
        Assert.AreEqual(1, provider.DisposeCount);

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAProviderMissingARequiredCapability(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler)
        {
            Capabilities = RequiredCapabilities & ~ExternalInteropCapabilities.SingleImmediateContextOrdering
        };

        _ = Assert.ThrowsException<NotSupportedException>(() => graphicsDevice.RegisterExternalDomain(provider));

        Assert.AreEqual(0, provider.InitializeCount);
        Assert.AreEqual(1, provider.DisposeCount);

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposesTheProviderWhenItsInitializationFails(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler) { ThrowOnInitialize = true };

        _ = Assert.ThrowsException<InvalidOperationException>(() => graphicsDevice.RegisterExternalDomain(provider));

        Assert.AreEqual(1, provider.InitializeCount);
        Assert.AreEqual(1, provider.DisposeCount);
        Assert.IsFalse(Windows.CloseHandle(new HANDLE((void*)provider.ObservedFenceHandle)));

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposingTheDomainReleasesTheProviderAndTheSchedulerRegistration(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        FakeInteropScheduler scheduler = new();
        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        scheduler.Dispose();

        Assert.AreEqual(0, scheduler.DisposeCount);
        Assert.AreEqual(0, provider.DisposeCount);

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposeRequested);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
        Assert.AreEqual(1, scheduler.DisposeCount);

        domain.WaitForDisposal();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposingTheDomainIsIdempotent(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        domain.Dispose();
        domain.Dispose();
        domain.Dispose();

        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void WaitingForTheDisposalOfALiveDomainIsRejected(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        _ = Assert.ThrowsException<InvalidOperationException>(domain.WaitForDisposal);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsARegistrationOnASchedulerThatReleasedItsOwnerReference(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        FakeInteropScheduler scheduler = new();

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        _ = Assert.ThrowsException<InvalidOperationException>(() => graphicsDevice.RegisterExternalDomain(provider));

        Assert.AreEqual(0, provider.InitializeCount);
        Assert.AreEqual(1, provider.DisposeCount);
    }
}
