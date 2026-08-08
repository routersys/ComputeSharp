using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeWeave.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ExternalFinalDrainTests
{
    private sealed class Fixture(GraphicsDevice device, ComputeSharedTextureInitialOwner initialOwner) : IDisposable
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
                InteropResourceSetRegistrationTests.ResourceSetDescriptor(1, initialOwner),
                [Slot]);

            return this;
        }

        public void Dispose()
        {
            // A failed assertion can leave a held signal behind, and the disposal below drains the external
            // queue, so the gate has to open before anything waits on it.
            Provider.ReleaseHeldSignals();

            Resources.Dispose();
            Resources.WaitForDisposal();
            Domain.Dispose();
            Scheduler.Dispose();
        }
    }

    private static Fixture Create(Device device, ComputeSharedTextureInitialOwner initialOwner)
    {
        return new Fixture(device.Get(), initialOwner).Register();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DrainsTheExternalQueueBeforeReleasingAnExternallyOwnedGeneration(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;

        Assert.AreEqual(0, fixture.Provider.SignalCount);

        fixture.Provider.HoldSignals();

        fixture.Slot.Dispose();

        Assert.AreEqual(1, fixture.Provider.SignalCount);
        Assert.AreEqual(1, fixture.Provider.FlushCount);
        Assert.IsTrue(fixture.Provider.WasReservedWhileSignaling);
        Assert.AreEqual(0, view.DisposeCount);

        fixture.Provider.ReleaseHeldSignals();

        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, view.DisposeCount);
        Assert.AreEqual(1, view.CompletedSignalsAtDispose);
        Assert.IsFalse(fixture.Scheduler.IsReserved);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SkipsTheDrainOfAGenerationTheExternalQueueNeverOwned(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;

        fixture.Slot.Dispose();

        Assert.AreEqual(0, fixture.Provider.SignalCount);
        Assert.AreEqual(1, view.DisposeCount);

        fixture.Slot.WaitForDisposal();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DrainsEveryReplacedGenerationOfASlot(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView first = fixture.Provider.LastOpenedView!;

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 16, out _));

        Assert.AreEqual(1, fixture.Provider.SignalCount);

        FakeExternalView second = fixture.Provider.LastOpenedView!;

        fixture.Slot.Dispose();
        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(2, fixture.Provider.SignalCount);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryPendingDrainWhenTheRegistryIsDisposed(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        SharedTextureSlot<Bgra32, Float4, FakeExternalView> slot = new();
        DeviceRegistrationRegistry registry = new(graphicsDevice, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        _ = registry.RegisterResourceSet(
            domain,
            InteropResourceSetRegistrationTests.ResourceSetDescriptor(1),
            [slot]);

        Assert.IsTrue(slot.TryEnsure(16, 16, out _));

        FakeExternalView view = provider.LastOpenedView!;

        provider.SignalDelayInMilliseconds = 50;

        registry.Dispose();

        Assert.AreEqual(1, provider.SignalCount);
        Assert.AreEqual(1, view.DisposeCount);
        Assert.AreEqual(1, view.CompletedSignalsAtDispose);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheResourceSetThroughTheCompletionCoordinator(Device device)
    {
        Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        try
        {
            Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

            FakeExternalView view = fixture.Provider.LastOpenedView!;

            fixture.Resources.Dispose();
            fixture.Resources.WaitForDisposal();

            Assert.AreEqual(1, fixture.Provider.SignalCount);
            Assert.AreEqual(1, view.DisposeCount);
            Assert.IsFalse(fixture.Scheduler.IsReserved);
        }
        finally
        {
            fixture.Domain.Dispose();
            fixture.Scheduler.Dispose();
        }
    }
}
