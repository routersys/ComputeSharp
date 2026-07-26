using System;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class MemoryAllocationCoordinatorTests
{
    private static SegmentPolicySnapshot Segment(ulong budgetBytes = 4096, ulong currentUsageBytes = 0)
    {
        return new SegmentPolicySnapshot
        {
            TopologyActive = true,
            DxgiStatus = MemoryBudgetStatus.Valid,
            Dxgi = new VideoMemoryBudgetSnapshot { BudgetBytes = budgetBytes, CurrentUsageBytes = currentUsageBytes },
            BrokerConfigured = false,
            GrantStatus = BrokerGrantStatus.NotConfigured
        };
    }

    private static VideoMemoryBudgetSnapshot Budget(ulong budgetBytes, ulong currentUsageBytes)
    {
        return new VideoMemoryBudgetSnapshot { BudgetBytes = budgetBytes, CurrentUsageBytes = currentUsageBytes };
    }

    [TestMethod]
    public void StartsWithANonZeroEpoch()
    {
        MemoryAllocationCoordinator coordinator = new();

        Assert.AreNotEqual(0ul, coordinator.Epoch);
        Assert.AreEqual(0, coordinator.LiveReservationCount);
    }

    [TestMethod]
    public void AdvancesTheEpochOnlyOnNewObservations()
    {
        MemoryAllocationCoordinator coordinator = new();

        ulong first = coordinator.ObserveBudget(MemoryPlacement.Local, Budget(4096, 128));

        Assert.IsTrue(first > 1);

        Assert.AreEqual(first, coordinator.ObserveBudget(MemoryPlacement.Local, Budget(4096, 128)));

        ulong second = coordinator.ObserveBudget(MemoryPlacement.Local, Budget(4096, 256));

        Assert.IsTrue(second > first);

        ulong third = coordinator.ObserveBudget(MemoryPlacement.NonLocal, Budget(4096, 256));

        Assert.IsTrue(third > second);
    }

    [TestMethod]
    public void RejectsReservationsBuiltOnAStaleSnapshot()
    {
        MemoryAllocationCoordinator coordinator = new();

        ulong epoch = coordinator.ObserveBudget(MemoryPlacement.Local, Budget(4096, 0));

        _ = coordinator.ObserveBudget(MemoryPlacement.Local, Budget(4096, 512));

        Assert.AreEqual(
            MemoryAdmissionStatus.StaleSnapshot,
            coordinator.TryReserve(MemoryPlacement.Local, Segment(), epoch, 128, out MemoryReservationToken token));

        Assert.IsTrue(token.IsNone);
        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);
    }

    [TestMethod]
    public void HoldsPendingBytesUntilTheReservationCommits()
    {
        MemoryAllocationCoordinator coordinator = new();

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 1024, out MemoryReservationToken token));

        Assert.IsFalse(token.IsNone);
        Assert.AreEqual(1, coordinator.LiveReservationCount);
        Assert.AreEqual(1024ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);
        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).OwnedBytes);

        coordinator.CommitReservation(token);

        Assert.AreEqual(0, coordinator.LiveReservationCount);
        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);
        Assert.AreEqual(1024ul, coordinator.GetAccounting(MemoryPlacement.Local).OwnedBytes);
    }

    [TestMethod]
    public void ReleasesPendingBytesWhenTheAllocationFails()
    {
        MemoryAllocationCoordinator coordinator = new();

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 1024, out MemoryReservationToken token);

        coordinator.AbortReservation(token);

        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);
        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).OwnedBytes);
        Assert.AreEqual(0, coordinator.LiveReservationCount);
    }

    [TestMethod]
    public void CountsPendingReservationsInLaterAdmissions()
    {
        MemoryAllocationCoordinator coordinator = new();

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, Segment(budgetBytes: 1024), coordinator.Epoch, 768, out _));

        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetExceeded,
            coordinator.TryReserve(MemoryPlacement.Local, Segment(budgetBytes: 1024), coordinator.Epoch, 512, out MemoryReservationToken rejected));

        Assert.IsTrue(rejected.IsNone);
        Assert.AreEqual(768ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);
    }

    [TestMethod]
    public void KeepsSegmentsIndependent()
    {
        MemoryAllocationCoordinator coordinator = new();

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 256, out MemoryReservationToken token);

        coordinator.CommitReservation(token);

        Assert.AreEqual(256ul, coordinator.GetAccounting(MemoryPlacement.Local).OwnedBytes);
        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.NonLocal).OwnedBytes);
    }

    [TestMethod]
    public void RejectsReusedAndUnknownReservationTokens()
    {
        MemoryAllocationCoordinator coordinator = new();

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 256, out MemoryReservationToken token);

        coordinator.CommitReservation(token);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => coordinator.CommitReservation(token));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => coordinator.AbortReservation(token));
        _ = Assert.ThrowsExactly<ArgumentException>(() => coordinator.AbortReservation(default));
    }

    [TestMethod]
    public void IssuesEveryReservationTokenExactlyOnce()
    {
        MemoryAllocationCoordinator coordinator = new();

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 16, out MemoryReservationToken first);
        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 16, out MemoryReservationToken second);

        Assert.AreNotEqual(first.Value, second.Value);

        coordinator.AbortReservation(first);

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 16, out MemoryReservationToken third);

        Assert.AreNotEqual(first.Value, third.Value);
        Assert.AreNotEqual(second.Value, third.Value);
    }

    [TestMethod]
    public void ReleasesOwnedBytesWithoutUnderflowing()
    {
        MemoryAllocationCoordinator coordinator = new();

        _ = coordinator.TryReserve(MemoryPlacement.Local, Segment(), coordinator.Epoch, 512, out MemoryReservationToken token);

        coordinator.CommitReservation(token);
        coordinator.ReleaseOwned(MemoryPlacement.Local, 512);

        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).OwnedBytes);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => coordinator.ReleaseOwned(MemoryPlacement.Local, 1));
    }

    [TestMethod]
    public void ClassifiesEveryNativeAllocationResult()
    {
        Assert.AreEqual(NativeAllocationOutcome.Succeeded, MemoryAllocationCoordinator.ClassifyNativeResult(S.S_OK));
        Assert.AreEqual(NativeAllocationOutcome.OutOfMemory, MemoryAllocationCoordinator.ClassifyNativeResult(E.E_OUTOFMEMORY));
        Assert.AreEqual(NativeAllocationOutcome.DeviceRemoved, MemoryAllocationCoordinator.ClassifyNativeResult(DXGI.DXGI_ERROR_DEVICE_REMOVED));
        Assert.AreEqual(NativeAllocationOutcome.DeviceRemoved, MemoryAllocationCoordinator.ClassifyNativeResult(DXGI.DXGI_ERROR_DEVICE_RESET));
        Assert.AreEqual(NativeAllocationOutcome.PlanValidationFailure, MemoryAllocationCoordinator.ClassifyNativeResult(E.E_INVALIDARG));
        Assert.AreEqual(NativeAllocationOutcome.Fault, MemoryAllocationCoordinator.ClassifyNativeResult(E.E_FAIL));
    }
}
