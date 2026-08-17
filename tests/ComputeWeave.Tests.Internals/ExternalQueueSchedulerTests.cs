using System;
using ComputeWeave.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ExternalQueueSchedulerTests
{
    private const string BuiltInEnter = "SingleReservationScheduler.EnterCore";

    private const string BuiltInExit = "SingleReservationScheduler.ExitCore";

    private const string MonitorEntry = "Monitor.Wait";

    private static readonly string[] MonitorMembers = ["Monitor.Enter", "Monitor.Exit", "Monitor.Wait", "Monitor.PulseAll"];

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

    private static SchedulerRegistration AcquireRegistration(ComputeExternalQueueScheduler scheduler)
    {
        Assert.IsTrue(SchedulerRegistration.TryAcquire(scheduler, out SchedulerRegistration? registration));

        return registration;
    }

    [TestMethod]
    public void TheBuiltInSchedulerAdmitsOneReservationAtATime()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        SchedulerRegistration first = AcquireRegistration(scheduler);
        SchedulerRegistration second = AcquireRegistration(scheduler);

        first.EnterReservation();

        _ = Assert.ThrowsException<InvalidOperationException>(second.EnterReservation);

        first.ExitReservation();

        second.EnterReservation();
        second.ExitReservation();

        first.Release();
        second.Release();
    }

    [TestMethod]
    public void TheBuiltInSchedulerRejectsAReservationReenteredOnTheSameThread()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.EnterReservation();

        _ = Assert.ThrowsException<InvalidOperationException>(registration.EnterReservation);

        registration.ExitReservation();
        registration.Release();
    }

    [TestMethod]
    public void TheBuiltInSchedulerRejectsAnExitWithoutAReservation()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        SchedulerRegistration registration = AcquireRegistration(scheduler);

        _ = Assert.ThrowsException<InvalidOperationException>(registration.ExitReservation);

        registration.Release();
    }

    [TestMethod]
    public void TheBuiltInSchedulerDoesNotPublishItsImplementationType()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        Type type = scheduler.GetType();

        Assert.AreNotSame(typeof(ComputeExternalQueueScheduler), type);
        Assert.IsFalse(type.IsVisible, type.FullName);
        Assert.AreNotSame(scheduler, ComputeExternalQueueScheduler.Create());
    }

    [TestMethod]
    public void TheBuiltInSchedulerReservesWithoutManagedAllocation()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        SchedulerRegistration registration = AcquireRegistration(scheduler);

        registration.EnterReservation();
        registration.ExitReservation();

        long minimum = long.MaxValue;

        for (int i = 0; i < 10; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int j = 0; j < 1000; j++)
            {
                registration.EnterReservation();
                registration.ExitReservation();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        registration.Release();

        Assert.AreEqual(0, minimum);
    }

    [TestMethod]
    public void TheBuiltInSchedulerReservesWithoutAMonitor()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(BuiltInEnter).Count, $"{BuiltInEnter} was not found in the assembly");
        Assert.AreNotEqual(0, graph.GetCallees(BuiltInExit).Count, $"{BuiltInExit} was not found in the assembly");
        Assert.IsTrue(
            graph.TryGetPath("ComputeInteropDomain.TryAcquireOperation", MonitorEntry, out _),
            "the call graph no longer resolves the primitive this test looks for");

        foreach (string root in new[] { BuiltInEnter, BuiltInExit })
        {
            foreach (string monitorMember in MonitorMembers)
            {
                Assert.IsFalse(
                    graph.TryGetPath(root, monitorMember, out string path),
                    $"the built-in scheduler reaches a monitor: {path}");
            }
        }
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
