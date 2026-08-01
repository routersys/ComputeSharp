using System;
using ComputeWeave.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ExternalQueueSchedulerTests
{
    private sealed class TrackingScheduler : ComputeExternalQueueScheduler
    {
        public int EnterCount;

        public int ExitCount;

        public int DisposeCount;

        public bool ThrowOnEnter;

        public bool ThrowOnExit;

        protected override void EnterCore()
        {
            if (this.ThrowOnEnter)
            {
                throw new InvalidOperationException("External queue scheduler is busy or reentered.");
            }

            this.EnterCount++;
        }

        protected override void ExitCore()
        {
            this.ExitCount++;

            if (this.ThrowOnExit)
            {
                throw new InvalidOperationException("Scheduler exit invariant failed.");
            }
        }

        protected override void DisposeCore()
        {
            this.DisposeCount++;
        }
    }

    private static SchedulerRegistration AcquireRegistration(TrackingScheduler scheduler)
    {
        Assert.IsTrue(SchedulerRegistration.TryAcquire(scheduler, out SchedulerRegistration? registration));

        return registration;
    }

    [TestMethod]
    public void ReleasingTheOwnerReferenceOfAnUnusedSchedulerDisposesIt()
    {
        TrackingScheduler scheduler = new();

        Assert.AreEqual(0, scheduler.DisposeCount);

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        TrackingScheduler scheduler = new();

        scheduler.Dispose();
        scheduler.Dispose();
        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void DisposeWaitsForEveryRegistrationToBeReleased()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration first = AcquireRegistration(scheduler);
        SchedulerRegistration second = AcquireRegistration(scheduler);

        scheduler.Dispose();

        Assert.AreEqual(0, scheduler.DisposeCount);

        first.Release();

        Assert.AreEqual(0, scheduler.DisposeCount);

        second.Release();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void DisposeWaitsForTheActiveReservationToExit()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.EnterReservation();
        scheduler.Dispose();
        registration.Release();

        Assert.AreEqual(0, scheduler.DisposeCount);

        registration.ExitReservation();

        Assert.AreEqual(1, scheduler.EnterCount);
        Assert.AreEqual(1, scheduler.ExitCount);
        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void ReleasedOwnerReferenceRejectsNewRegistrations()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        scheduler.Dispose();

        Assert.IsFalse(SchedulerRegistration.TryAcquire(scheduler, out SchedulerRegistration? rejected));
        Assert.IsNull(rejected);

        registration.Release();
    }

    [TestMethod]
    public void ReleasedOwnerReferenceDoesNotRejectHeldRegistrations()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        scheduler.Dispose();

        registration.EnterReservation();
        registration.ExitReservation();

        Assert.AreEqual(1, scheduler.EnterCount);
        Assert.AreEqual(1, scheduler.ExitCount);
        Assert.AreEqual(0, scheduler.DisposeCount);

        registration.Release();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void RegistrationReleaseIsIdempotent()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.Release();
        registration.Release();

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void AReleasedRegistrationDoesNotReserveTheExternalQueue()
    {
        TrackingScheduler scheduler = new();
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.Release();

        _ = Assert.ThrowsException<InvalidOperationException>(registration.EnterReservation);
        Assert.AreEqual(0, scheduler.EnterCount);
    }

    [TestMethod]
    public void ReservingTheExternalQueueWithoutARegistrationIsRejected()
    {
        TrackingScheduler scheduler = new();

        _ = Assert.ThrowsException<InvalidOperationException>(scheduler.EnterReservation);

        Assert.AreEqual(0, scheduler.EnterCount);

        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void AFailedEnterCoreLeavesNoActiveReservationBehind()
    {
        TrackingScheduler scheduler = new() { ThrowOnEnter = true };
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        _ = Assert.ThrowsException<InvalidOperationException>(registration.EnterReservation);

        registration.Release();
        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [TestMethod]
    public void AFailedExitCoreStillReleasesTheReservation()
    {
        TrackingScheduler scheduler = new() { ThrowOnExit = true };
        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.EnterReservation();

        _ = Assert.ThrowsException<InvalidOperationException>(registration.ExitReservation);

        registration.Release();
        scheduler.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }
}
