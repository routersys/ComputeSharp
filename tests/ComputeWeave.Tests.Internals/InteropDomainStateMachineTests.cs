using ComputeWeave.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class InteropDomainStateMachineTests
{
    private static ComputeInteropDomainRecord TeardownStartedDomain()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryRequestDispose());
        Assert.IsTrue(domain.TryReleaseOwner());
        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, domain.State);

        return domain;
    }

    [TestMethod]
    public void DomainStartsActiveWithOnlyTheOwnerReference()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.AreEqual(ComputeInteropDomainState.Active, domain.State);
        Assert.AreEqual(1, domain.References.Owner);
        Assert.AreEqual(0, domain.References.ResourceSet);
        Assert.AreEqual(0, domain.References.PersistentLease);
        Assert.AreEqual(0, domain.References.TransientOperation);
        Assert.AreEqual(0, domain.References.PendingTransaction);
        Assert.AreEqual(0, domain.References.Maintenance);
        Assert.IsFalse(domain.References.IsZero);
        Assert.IsFalse(domain.IsDisposeRequested);
        Assert.IsFalse(domain.IsDisposed);
    }

    [TestMethod]
    public void DisposeRequestIsFollowedByTeardownWhenTheOwnerIsReleased()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryRequestDispose());
        Assert.AreEqual(ComputeInteropDomainState.DisposeRequested, domain.State);
        Assert.IsFalse(domain.IsDisposeRequested);

        Assert.IsTrue(domain.TryReleaseOwner());
        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, domain.State);
        Assert.IsTrue(domain.IsDisposeRequested);
    }

    [TestMethod]
    public void DisposeRequestAndOwnerReleaseAreBothIdempotent()
    {
        ComputeInteropDomainRecord domain = TeardownStartedDomain();

        Assert.IsFalse(domain.TryRequestDispose());
        Assert.IsFalse(domain.TryReleaseOwner());
        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, domain.State);
    }

    [TestMethod]
    public void OnlyAnActiveDomainAcceptsNewReferences()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.ResourceSet));
        Assert.IsTrue(domain.TryRequestDispose());
        Assert.IsFalse(domain.TryAcquire(ExternalDomainReference.ResourceSet));
        Assert.AreEqual(1, domain.References.ResourceSet);
    }

    [TestMethod]
    public void EveryReferenceKindIsCountedOnItsOwnField()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.Owner));
        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.ResourceSet));
        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.PersistentLease));
        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.TransientOperation));
        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.PendingTransaction));
        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.Maintenance));

        Assert.AreEqual(2, domain.References.Owner);
        Assert.AreEqual(1, domain.References.ResourceSet);
        Assert.AreEqual(1, domain.References.PersistentLease);
        Assert.AreEqual(1, domain.References.TransientOperation);
        Assert.AreEqual(1, domain.References.PendingTransaction);
        Assert.AreEqual(1, domain.References.Maintenance);

        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.Owner));
        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.ResourceSet));
        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.PersistentLease));
        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.TransientOperation));
        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.PendingTransaction));
        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.Maintenance));

        Assert.AreEqual(1, domain.References.Owner);
        Assert.IsFalse(domain.References.IsZero);
    }

    [TestMethod]
    public void ReleasingAReferenceThatIsNotHeldFails()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsFalse(domain.TryRelease(ExternalDomainReference.ResourceSet));
    }

    [TestMethod]
    public void ExhaustedReferenceCountsAreNotAcquired()
    {
        DomainReferenceCounts counts = new()
        {
            ResourceSet = int.MaxValue
        };

        Assert.IsFalse(counts.TryAcquire(ExternalDomainReference.ResourceSet));
        Assert.AreEqual(int.MaxValue, counts.ResourceSet);
    }

    [TestMethod]
    public void PoisonAppliesToAnActiveOrDisposeRequestedDomain()
    {
        ComputeInteropDomainRecord active = new();

        Assert.IsTrue(active.TryMarkPoisoned());
        Assert.AreEqual(ComputeInteropDomainState.Poisoned, active.State);

        ComputeInteropDomainRecord disposeRequested = new();

        Assert.IsTrue(disposeRequested.TryRequestDispose());
        Assert.IsTrue(disposeRequested.TryMarkPoisoned());
        Assert.AreEqual(ComputeInteropDomainState.Poisoned, disposeRequested.State);

        ComputeInteropDomainRecord teardownStarted = TeardownStartedDomain();

        Assert.IsFalse(teardownStarted.TryMarkPoisoned());
        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, teardownStarted.State);
    }

    [TestMethod]
    public void PoisonBeginsTeardownWithoutWaitingForReferences()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.ResourceSet));
        Assert.IsTrue(domain.TryMarkPoisoned());
        Assert.IsTrue(domain.TryBeginTeardown());

        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, domain.State);
        Assert.IsFalse(domain.References.IsZero);
    }

    [TestMethod]
    public void PoisonedTeardownStillWaitsForEveryReferenceBeforeReleasingNative()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryMarkPoisoned());
        Assert.IsTrue(domain.TryBeginTeardown());
        Assert.IsFalse(domain.TryBeginReleasingNative());

        Assert.IsTrue(domain.TryReleaseOwner());
        Assert.IsTrue(domain.TryBeginReleasingNative());
        Assert.AreEqual(ComputeInteropDomainState.ReleasingNative, domain.State);
    }

    [TestMethod]
    public void TeardownDoesNotReleaseNativeWhileAnyReferenceIsHeld()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.Maintenance));
        Assert.IsTrue(domain.TryRequestDispose());
        Assert.IsTrue(domain.TryReleaseOwner());
        Assert.IsFalse(domain.TryBeginReleasingNative());

        Assert.IsTrue(domain.TryRelease(ExternalDomainReference.Maintenance));
        Assert.IsTrue(domain.TryBeginReleasingNative());
    }

    [TestMethod]
    public void DeviceTerminalAppliesToEveryStateBeforeTheNativeRelease()
    {
        ComputeInteropDomainRecord active = new();

        Assert.IsTrue(active.TryMarkTerminal());
        Assert.AreEqual(ComputeInteropDomainState.Terminal, active.State);

        ComputeInteropDomainRecord disposeRequested = new();

        Assert.IsTrue(disposeRequested.TryRequestDispose());
        Assert.IsTrue(disposeRequested.TryMarkTerminal());

        ComputeInteropDomainRecord poisoned = new();

        Assert.IsTrue(poisoned.TryMarkPoisoned());
        Assert.IsTrue(poisoned.TryMarkTerminal());

        ComputeInteropDomainRecord teardownStarted = TeardownStartedDomain();

        Assert.IsTrue(teardownStarted.TryMarkTerminal());
        Assert.IsFalse(teardownStarted.TryMarkTerminal());
    }

    [TestMethod]
    public void DeviceTerminalDoesNotReopenADomainThatIsReleasingItsNativeObjects()
    {
        ComputeInteropDomainRecord domain = TeardownStartedDomain();

        Assert.IsTrue(domain.TryBeginReleasingNative());
        Assert.IsFalse(domain.TryMarkTerminal());
        Assert.AreEqual(ComputeInteropDomainState.ReleasingNative, domain.State);

        Assert.IsTrue(domain.TryCompleteDisposal());
        Assert.IsFalse(domain.TryMarkTerminal());
        Assert.AreEqual(ComputeInteropDomainState.Disposed, domain.State);
    }

    [TestMethod]
    public void DeviceTeardownReleasesTheNativeObjectsOfATerminalDomain()
    {
        ComputeInteropDomainRecord domain = new();

        Assert.IsTrue(domain.TryAcquire(ExternalDomainReference.PersistentLease));
        Assert.IsTrue(domain.TryMarkTerminal());
        Assert.IsFalse(domain.TryBeginReleasingNative());

        Assert.IsTrue(domain.TryBeginReleasingNativeForDeviceTeardown());
        Assert.AreEqual(ComputeInteropDomainState.ReleasingNative, domain.State);
    }

    [TestMethod]
    public void DeviceTeardownOnlyReleasesATerminalDomain()
    {
        ComputeInteropDomainRecord domain = TeardownStartedDomain();

        Assert.IsFalse(domain.TryBeginReleasingNativeForDeviceTeardown());
        Assert.AreEqual(ComputeInteropDomainState.TeardownStarted, domain.State);
    }

    [TestMethod]
    public void DisposalCompletesOnlyFromTheNativeRelease()
    {
        ComputeInteropDomainRecord domain = TeardownStartedDomain();

        Assert.IsFalse(domain.TryCompleteDisposal());

        Assert.IsTrue(domain.TryBeginReleasingNative());
        Assert.IsTrue(domain.TryCompleteDisposal());
        Assert.IsTrue(domain.IsDisposed);

        Assert.IsFalse(domain.TryCompleteDisposal());
    }
}
