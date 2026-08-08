using System;
using System.Threading.Tasks;
using ComputeWeave.Resources.Interop;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
