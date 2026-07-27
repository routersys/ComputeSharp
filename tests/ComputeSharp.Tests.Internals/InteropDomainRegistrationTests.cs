using System;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2213

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class InteropDomainRegistrationTests
{
    private const ExternalInteropCapabilities RequiredCapabilities =
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering;

    private sealed class FakeExternalView : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class FakeScheduler : ComputeExternalQueueScheduler
    {
        public int DisposeCount;

        protected override void EnterCore()
        {
        }

        protected override void ExitCore()
        {
        }

        protected override void DisposeCore()
        {
            this.DisposeCount++;
        }
    }

    private sealed class FakeProvider(GraphicsDevice device, FakeScheduler scheduler)
        : IComputeExternalInteropProvider<FakeExternalView>
    {
        private readonly GraphicsDevice device = device;

        private readonly FakeScheduler scheduler = scheduler;

        public ExternalAdapterIdentity AdapterIdentity { get; set; } = new ExternalAdapterIdentity(device.Luid.ToInt64());

        public ExternalInteropCapabilities Capabilities { get; set; } = RequiredCapabilities;

        public ComputeExternalQueueScheduler Scheduler => this.scheduler;

        public bool ThrowOnInitialize { get; set; }

        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public nint ObservedFenceHandle { get; private set; }

        public bool OpenedSharedFence { get; private set; }

        public void Initialize(in ExternalTimelineInitialization initialization)
        {
            this.InitializeCount++;
            this.ObservedFenceHandle = initialization.SharedFenceHandle.DangerousGetHandle();

            using ComPtr<ID3D12Fence> d3D12Fence = this.device.OpenSharedFence(new HANDLE((void*)this.ObservedFenceHandle));

            this.OpenedSharedFence = d3D12Fence.Get() is not null;

            if (this.ThrowOnInitialize)
            {
                throw new InvalidOperationException("The provider could not open the shared timeline.");
            }
        }

        public void EnqueueSignal(ulong value)
        {
        }

        public void FlushAfterSignal()
        {
        }

        public void EnqueueWait(ulong value)
        {
        }

        public FakeExternalView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
        {
            return new FakeExternalView();
        }

        public void OnDeviceTerminal(Exception reason)
        {
        }

        public void Dispose()
        {
            this.DisposeCount++;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RegistersADomainAndInitializesItsProvider(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler);

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

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreNotEqual(0, provider.ObservedFenceHandle);
        Assert.IsFalse(Windows.CloseHandle(new HANDLE((void*)provider.ObservedFenceHandle)));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AssignsADistinctIdentifierToEveryDomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeScheduler scheduler = new();

        using ComputeInteropDomain first = graphicsDevice.RegisterExternalDomain(new FakeProvider(graphicsDevice, scheduler));
        using ComputeInteropDomain second = graphicsDevice.RegisterExternalDomain(new FakeProvider(graphicsDevice, scheduler));

        Assert.AreNotEqual(first.Id.Value, second.Id.Value);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAProviderBoundToAnotherAdapter(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler)
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

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler)
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

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler) { ThrowOnInitialize = true };

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

        FakeScheduler scheduler = new();
        FakeProvider provider = new(graphicsDevice, scheduler);

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

        using FakeScheduler scheduler = new();

        FakeProvider provider = new(graphicsDevice, scheduler);

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

        using FakeScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeProvider(graphicsDevice, scheduler));

        _ = Assert.ThrowsException<InvalidOperationException>(domain.WaitForDisposal);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsARegistrationOnASchedulerThatReleasedItsOwnerReference(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        FakeScheduler scheduler = new();

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);

        FakeProvider provider = new(graphicsDevice, scheduler);

        _ = Assert.ThrowsException<InvalidOperationException>(() => graphicsDevice.RegisterExternalDomain(provider));

        Assert.AreEqual(0, provider.InitializeCount);
        Assert.AreEqual(1, provider.DisposeCount);
    }
}
