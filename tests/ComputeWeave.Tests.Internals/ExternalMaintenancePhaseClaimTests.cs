using System;
using System.Threading.Tasks;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Interop;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeWeave.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Covers the phase claim of the external drain maintenance record.
/// </summary>
/// <remarks>
/// Section 3 of the external drain maintenance specification requires that at most one pass runs a phase of a
/// record at a time. The phase runs outside the maintenance exclusion and the record only advances after the
/// external queue has already been signalled, so without the claim two passes both issue the final drain.
/// </remarks>
[TestClass]
public class ExternalMaintenancePhaseClaimTests
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
            Provider.ReleaseBlockedEnqueue();
            Provider.ReleaseHeldSignals();

            Resources.Dispose();
            Resources.WaitForDisposal();
            Domain.Dispose();
            Scheduler.Dispose();
        }
    }

    [TestMethod]
    public void ARecordGrantsItsPhaseClaimToOneHolder()
    {
        ExternalMaintenanceRecord record = new(default, default, default);

        Assert.IsTrue(record.TryBeginPhase());
        Assert.IsFalse(record.TryBeginPhase());

        Assert.IsTrue(record.TryEndPhase());
        Assert.IsFalse(record.TryEndPhase());

        Assert.IsTrue(record.TryBeginPhase());
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RunsOneFinalDrainWhenASecondPassOverlapsTheFirst(Device device)
    {
        using Fixture fixture = new Fixture(device.Get(), ComputeSharedTextureInitialOwner.External).Register();

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        fixture.Provider.BlockNextEnqueue();

        Task first = Task.Run(fixture.Slot.Dispose);

        Assert.IsTrue(fixture.Provider.WaitForBlockedEnqueue(), "no pass entered the phase");

        // A pass holds the claim and sits inside its phase. Every pass running beside it has to leave the
        // record alone rather than issue a second final drain of the same generation.
        for (int i = 0; i < 32; i++)
        {
            ((IComputeSharedSlot)fixture.Slot).RunMaintenance();
        }

        Assert.AreEqual(1, fixture.Provider.SignalCount, "signal count while a pass holds the claim");

        fixture.Provider.ReleaseBlockedEnqueue();

        first.Wait();

        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, fixture.Provider.SignalCount, "signal count after the claim was released");
        Assert.AreEqual(1, fixture.Provider.LastOpenedView!.DisposeCount, "view dispose count");
        Assert.IsFalse(fixture.Scheduler.IsReserved);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesThePhaseClaimAfterAPassThatIssuedNoDrain(Device device)
    {
        using Fixture fixture = new Fixture(device.Get(), ComputeSharedTextureInitialOwner.External).Register();

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        // A pass that finds nothing to do still has to leave the claim behind it, or the disposal below never
        // reaches a phase again.
        for (int i = 0; i < 8; i++)
        {
            ((IComputeSharedSlot)fixture.Slot).RunMaintenance();
        }

        fixture.Slot.Dispose();
        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, fixture.Provider.SignalCount, "signal count");
        Assert.AreEqual(1, fixture.Provider.LastOpenedView!.DisposeCount, "view dispose count");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RunsOneFinalDrainWhenATeardownOverlapsTheCoordinator(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        SharedTextureSlot<Bgra32, Float4, FakeExternalView> slot = new();
        DeviceRegistrationRegistry registry = new(graphicsDevice, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        _ = registry.RegisterResourceSet(
            domain,
            InteropResourceSetRegistrationTests.ResourceSetDescriptor(1, ComputeSharedTextureInitialOwner.External),
            [slot]);

        Assert.IsTrue(slot.TryEnsure(16, 16, out _));

        FakeExternalView view = provider.LastOpenedView!;

        provider.BlockNextEnqueue();

        // The teardown becomes the executor while its own coordinator is still running. Only the phase claim
        // keeps the two from issuing the same final drain.
        Task teardown = Task.Run(registry.Dispose);

        Assert.IsTrue(provider.WaitForBlockedEnqueue(), "the teardown never entered the phase");

        for (int i = 0; i < 32; i++)
        {
            registry.Coordinator.Wake();
        }

        Assert.AreEqual(1, provider.SignalCount, "signal count while the teardown holds the claim");

        provider.ReleaseBlockedEnqueue();

        teardown.Wait();

        Assert.AreEqual(1, provider.SignalCount, "signal count after the teardown finished");
        Assert.AreEqual(1, view.DisposeCount, "view dispose count");
        Assert.AreEqual(1, view.CompletedSignalsAtDispose, "completed signals at dispose");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesThePhaseToAPassThatReentersOnTheSameThread(Device device)
    {
        using Fixture fixture = new Fixture(device.Get(), ComputeSharedTextureInitialOwner.External).Register();

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        int reentrantSignalCount = -1;

        // Runs on the thread that already holds the claim. A claim that let its own holder back in would issue
        // a second final drain from inside the first.
        fixture.Provider.OnEnqueueSignal = () =>
        {
            ((IComputeSharedSlot)fixture.Slot).RunMaintenance();

            reentrantSignalCount = fixture.Provider.SignalCount;
        };

        fixture.Slot.Dispose();

        ExternalMaintenanceWait.WaitFor(
            device.Get(),
            () => fixture.Provider.FlushCount == 1,
            "the external queue was signalled and flushed");

        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, reentrantSignalCount, "signal count observed from inside the phase");
        Assert.AreEqual(1, fixture.Provider.SignalCount, "signal count");
        Assert.AreEqual(1, fixture.Provider.LastOpenedView!.DisposeCount, "view dispose count");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClaimsThePhaseOfAGenerationThatNeedsNoFinalDrain(Device device)
    {
        using Fixture fixture = new Fixture(device.Get(), ComputeSharedTextureInitialOwner.Compute).Register();

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;

        // The external queue never owned this generation, so the pass skips the final drain and goes straight
        // to the external release. That path takes the claim through a different branch of the entry, and a
        // branch that returns without claiming would release a claim it never held.
        fixture.Slot.Dispose();
        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(0, fixture.Provider.SignalCount, "signal count");
        Assert.AreEqual(1, view.DisposeCount, "view dispose count");
        Assert.IsFalse(fixture.Scheduler.IsReserved);
    }
}
