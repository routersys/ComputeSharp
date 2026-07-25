using System;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class RegistrationStateMachineTests
{
    private static HostRegistrationRecord ActiveHost(int maximumConcurrentInvocations = 1, int maximumPendingSubmissions = 2)
    {
        HostRegistrationRecord host = new(
            new HostRegistrationId(1),
            maximumConcurrentInvocations,
            maximumPendingSubmissions,
            maximumTrackedResourceCount: 4,
            maximumCommandListSegments: 1,
            ownedSlotCount: 1);

        Assert.IsTrue(host.TryCommitActive());

        return host;
    }

    private static ResourceSetRegistrationRecord ActiveResourceSet(int sharedTextureSlotCount = 2)
    {
        ResourceSetRegistrationRecord resourceSet = new(new ResourceSetRegistrationId(1), sharedTextureSlotCount);

        Assert.IsTrue(resourceSet.TryCommitActive());

        return resourceSet;
    }

    [TestMethod]
    public void HostRejectsZeroIdentity()
    {
        _ = Assert.ThrowsException<ArgumentException>(static () => new HostRegistrationRecord(default, 1, 1, 0, 0, 0));
    }

    [TestMethod]
    public void HostRejectsPendingSubmissionsBelowConcurrentInvocations()
    {
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(
            static () => new HostRegistrationRecord(new HostRegistrationId(1), 2, 1, 0, 0, 0));
    }

    [TestMethod]
    public void HostStartsConstructing()
    {
        HostRegistrationRecord host = new(new HostRegistrationId(1), 1, 1, 0, 0, 0);

        Assert.AreEqual(RegistrationState.Constructing, host.State);
        Assert.IsFalse(host.TryAcquireInvocation());
        Assert.IsFalse(host.TryReservePendingSubmission());
    }

    [TestMethod]
    public void HostAbortedConstructionIsReleased()
    {
        HostRegistrationRecord host = new(new HostRegistrationId(1), 1, 1, 0, 0, 0);

        Assert.IsTrue(host.TryAbortConstruction());
        Assert.AreEqual(RegistrationState.Released, host.State);
        Assert.IsFalse(host.TryCommitActive());
    }

    [TestMethod]
    public void HostInvocationsAreBoundedByContract()
    {
        HostRegistrationRecord host = ActiveHost(maximumConcurrentInvocations: 2, maximumPendingSubmissions: 3);

        Assert.IsTrue(host.TryAcquireInvocation());
        Assert.IsTrue(host.TryAcquireInvocation());
        Assert.IsFalse(host.TryAcquireInvocation());

        host.ReleaseInvocation();

        Assert.IsTrue(host.TryAcquireInvocation());
    }

    [TestMethod]
    public void HostPendingSubmissionsAreBoundedByContract()
    {
        HostRegistrationRecord host = ActiveHost(maximumConcurrentInvocations: 1, maximumPendingSubmissions: 2);

        Assert.IsTrue(host.TryReservePendingSubmission());
        Assert.IsTrue(host.TryReservePendingSubmission());
        Assert.IsFalse(host.TryReservePendingSubmission());

        host.ReleasePendingSubmission();

        Assert.IsTrue(host.TryReservePendingSubmission());
    }

    [TestMethod]
    public void HostReleaseWithoutReservationThrows()
    {
        HostRegistrationRecord host = ActiveHost();

        _ = Assert.ThrowsException<InvalidOperationException>(() => host.ReleaseInvocation());
        _ = Assert.ThrowsException<InvalidOperationException>(() => host.ReleasePendingSubmission());
    }

    [TestMethod]
    public void HostDisposeRequestRejectsNewWork()
    {
        HostRegistrationRecord host = ActiveHost();

        Assert.IsTrue(host.TryRequestDispose());
        Assert.IsFalse(host.TryRequestDispose());
        Assert.IsFalse(host.TryAcquireInvocation());
        Assert.IsFalse(host.TryReservePendingSubmission());
    }

    [TestMethod]
    public void HostReleaseRequiresEveryCompletionCondition()
    {
        HostRegistrationRecord host = ActiveHost();

        Assert.IsTrue(host.TryAcquireInvocation());
        Assert.IsTrue(host.TryReservePendingSubmission());
        Assert.IsFalse(host.TryBeginRelease(isOwnedSlotDisposalComplete: true));

        Assert.IsTrue(host.TryRequestDispose());
        Assert.IsFalse(host.TryBeginRelease(isOwnedSlotDisposalComplete: true));

        host.ReleaseInvocation();

        Assert.IsFalse(host.TryBeginRelease(isOwnedSlotDisposalComplete: true));

        host.ReleasePendingSubmission();

        Assert.IsFalse(host.TryBeginRelease(isOwnedSlotDisposalComplete: false));
        Assert.IsTrue(host.TryBeginRelease(isOwnedSlotDisposalComplete: true));
        Assert.IsTrue(host.TryCompleteRelease());
        Assert.AreEqual(RegistrationState.Released, host.State);
        Assert.IsFalse(host.TryCompleteRelease());
    }

    [TestMethod]
    public void ResourceSetRejectsZeroIdentity()
    {
        _ = Assert.ThrowsException<ArgumentException>(static () => new ResourceSetRegistrationRecord(default, 1));
    }

    [TestMethod]
    public void ResourceSetPersistentLeasesAreBoundedByTwiceTheSlotCount()
    {
        ResourceSetRegistrationRecord resourceSet = ActiveResourceSet(sharedTextureSlotCount: 1);

        Assert.IsTrue(resourceSet.TryAcquirePersistentLease());
        Assert.IsTrue(resourceSet.TryAcquirePersistentLease());
        Assert.IsFalse(resourceSet.TryAcquirePersistentLease());

        resourceSet.ReleasePersistentLease();

        Assert.IsTrue(resourceSet.TryAcquirePersistentLease());
    }

    [TestMethod]
    public void ResourceSetMaintenanceIsBoundedBySlotCount()
    {
        ResourceSetRegistrationRecord resourceSet = ActiveResourceSet(sharedTextureSlotCount: 1);

        Assert.IsTrue(resourceSet.TryRegisterMaintenance());
        Assert.IsFalse(resourceSet.TryRegisterMaintenance());

        resourceSet.CompleteMaintenance();

        Assert.IsTrue(resourceSet.TryRegisterMaintenance());
    }

    [TestMethod]
    public void ResourceSetMaintenanceRemainsAvailableAfterDisposeRequest()
    {
        ResourceSetRegistrationRecord resourceSet = ActiveResourceSet(sharedTextureSlotCount: 1);

        Assert.IsTrue(resourceSet.TryRequestDispose());
        Assert.IsFalse(resourceSet.TryAcquirePersistentLease());
        Assert.IsTrue(resourceSet.TryRegisterMaintenance());
    }

    [TestMethod]
    public void ResourceSetReleaseRequiresEveryCompletionCondition()
    {
        ResourceSetRegistrationRecord resourceSet = ActiveResourceSet(sharedTextureSlotCount: 1);

        Assert.IsTrue(resourceSet.TryAcquirePersistentLease());
        Assert.IsTrue(resourceSet.TryRequestDispose());
        Assert.IsTrue(resourceSet.TryRegisterMaintenance());
        Assert.IsFalse(resourceSet.TryBeginRelease(isSharedSlotDisposalComplete: true));

        resourceSet.ReleasePersistentLease();

        Assert.IsFalse(resourceSet.TryBeginRelease(isSharedSlotDisposalComplete: true));

        resourceSet.CompleteMaintenance();

        Assert.IsFalse(resourceSet.TryBeginRelease(isSharedSlotDisposalComplete: false));
        Assert.IsTrue(resourceSet.TryBeginRelease(isSharedSlotDisposalComplete: true));
        Assert.IsFalse(resourceSet.TryRegisterMaintenance());
        Assert.IsTrue(resourceSet.TryCompleteRelease());
        Assert.AreEqual(RegistrationState.Released, resourceSet.State);
    }
}
