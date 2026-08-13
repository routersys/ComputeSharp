using System;
using System.Reflection;
using System.Threading;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class InteropDomainOperationTests
{
    private static void WaitForBlockedWait(Thread thread)
    {
        Assert.IsTrue(SpinWait.SpinUntil(
            () => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
            TimeSpan.FromSeconds(5)));
    }

    private static DomainOperationStatus Acquire(ComputeInteropDomain domain, out DomainOperationLease lease)
    {
        return domain.TryAcquireOperation(
            ExternalDomainReference.TransientOperation,
            default,
            releaseExternalReferenceOnDispose: false,
            out lease,
            out _);
    }

    [TestMethod]
    public void OperationTokensAreNonZeroAndMonotonic()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong first));
        Assert.AreNotEqual(0ul, first);
        Assert.IsTrue(record.TryRelease(first));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong second));
        Assert.IsTrue(second > first);
    }

    [TestMethod]
    public void ThePermitAdmitsOneOperationAtATime()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong token));
        Assert.AreEqual(DomainOperationStatus.PermitBusy, record.TryAcquire(default, false, out ulong rejected));
        Assert.AreEqual(0ul, rejected);

        Assert.IsTrue(record.TryRelease(token));
        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out _));
    }

    [TestMethod]
    public void APermitThatIsReleasingIsNotReacquired()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong token));
        Assert.IsTrue(record.TryBeginRelease(token));
        Assert.AreEqual(DomainOperationStatus.PermitBusy, record.TryAcquire(default, false, out _));

        record.CompleteRelease();

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out _));
    }

    [TestMethod]
    public void OnlyTheAcquiringTokenReleasesTheOperation()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong token));

        Assert.IsFalse(record.TryRelease(0));
        Assert.IsFalse(record.TryRelease(token + 1));
        Assert.IsTrue(record.TryRelease(token));
        Assert.IsFalse(record.TryRelease(token));
    }

    [TestMethod]
    public void AStaleTokenDoesNotReleaseANewerOperation()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1));

        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong stale));
        Assert.IsTrue(record.TryRelease(stale));
        Assert.AreEqual(DomainOperationStatus.Acquired, record.TryAcquire(default, false, out ulong current));

        Assert.IsFalse(record.TryRelease(stale));
        Assert.IsTrue(record.TryRelease(current));
    }

    [TestMethod]
    public void AnExhaustedTokenSequenceIsReportedBeforeThePermit()
    {
        DomainOperationRecord record = new(new ExternalDomainId(1))
        {
            NextToken = ulong.MaxValue
        };

        Assert.AreEqual(DomainOperationStatus.TokenExhausted, record.TryAcquire(default, false, out ulong token));
        Assert.AreEqual(0ul, token);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquiringAnOperationReservesTheExternalQueueOnce(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease lease));
        Assert.IsTrue(lease.IsValid);
        Assert.AreEqual(1, scheduler.EnterCount);
        Assert.AreEqual(0, scheduler.ExitCount);

        lease.Dispose();

        Assert.AreEqual(1, scheduler.ExitCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnActiveOperationHoldsTheDomainUntilItIsReleased(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease lease));

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposeRequested);
        Assert.IsFalse(domain.IsDisposed);
        Assert.AreEqual(0, provider.DisposeCount);

        lease.Dispose();

        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ASecondOperationIsRejectedWhileOneIsActive(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease lease));
        Assert.AreEqual(DomainOperationStatus.PermitBusy, Acquire(domain, out DomainOperationLease rejected));
        Assert.IsFalse(rejected.IsValid);
        Assert.AreEqual(1, scheduler.EnterCount);

        lease.Dispose();

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease next));

        next.Dispose();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AForegroundOperationWaitsForMaintenance(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease maintenance,
            out _);

        Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

        DomainOperationStatus attemptStatus = DomainOperationStatus.DomainUnavailable;
        DomainOperationLease attemptLease = default;
        Thread attempt = new(() => attemptStatus = Acquire(domain, out attemptLease));

        attempt.Start();

        try
        {
            WaitForBlockedWait(attempt);
        }
        finally
        {
            maintenance.Dispose();
        }

        Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(DomainOperationStatus.Acquired, attemptStatus);
        Assert.IsTrue(attemptLease.IsValid);

        attemptLease.Dispose();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AForegroundOperationWaitingForMaintenanceObservesDisposal(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease maintenance,
            out _);

        Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

        DomainOperationStatus attemptStatus = DomainOperationStatus.Acquired;
        DomainOperationLease attemptLease = default;
        Thread attempt = new(() => attemptStatus = Acquire(domain, out attemptLease));

        attempt.Start();

        try
        {
            WaitForBlockedWait(attempt);

            domain.Dispose();
        }
        finally
        {
            maintenance.Dispose();
        }

        Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(DomainOperationStatus.DomainUnavailable, attemptStatus);
        Assert.IsFalse(attemptLease.IsValid);

        domain.WaitForDisposal();

        Assert.IsTrue(domain.IsDisposed);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnInterruptedForegroundWaitReleasesItsDomainReference(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease maintenance,
            out _);

        Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

        Exception? failure = null;

        Thread attempt = new(() =>
        {
            try
            {
                _ = Acquire(domain, out _);
            }
            catch (Exception e)
            {
                failure = e;
            }
        });

        attempt.Start();

        try
        {
            WaitForBlockedWait(attempt);

            attempt.Interrupt();

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
            Assert.IsInstanceOfType<ThreadInterruptedException>(failure);
        }
        finally
        {
            maintenance.Dispose();

            if (attempt.IsAlive)
            {
                attempt.Interrupt();
                _ = attempt.Join(TimeSpan.FromSeconds(5));
            }
        }

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease next));

        next.Dispose();

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnInterruptedWaitRegistrationReleasesItsDomainReference(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreEqual(
            DomainOperationStatus.Acquired,
            domain.TryAcquireOperation(
                ExternalDomainReference.Maintenance,
                default,
                releaseExternalReferenceOnDispose: false,
                out DomainOperationLease maintenance,
                out _));

        DomainOperationStatus firstStatus = DomainOperationStatus.DomainUnavailable;
        DomainOperationLease firstLease = default;
        Thread first = new(() => firstStatus = Acquire(domain, out firstLease));

        first.Start();
        WaitForBlockedWait(first);

        FieldInfo releaseGateField = typeof(ComputeInteropDomain).GetField(
            "operationReleaseGate",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object releaseGate = releaseGateField.GetValue(domain)!;
        Exception? failure = null;
        Thread second = new(() =>
        {
            try
            {
                _ = Acquire(domain, out _);
            }
            catch (Exception e)
            {
                failure = e;
            }
        });

        try
        {
            lock (releaseGate)
            {
                second.Start();
                WaitForBlockedWait(second);

                second.Interrupt();

                Assert.IsTrue(second.Join(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            maintenance.Dispose();

            if (second.IsAlive)
            {
                second.Interrupt();
                _ = second.Join(TimeSpan.FromSeconds(5));
            }
        }

        Assert.IsTrue(first.Join(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(DomainOperationStatus.Acquired, firstStatus);
        Assert.IsTrue(firstLease.IsValid);
        Assert.IsInstanceOfType<ThreadInterruptedException>(failure);

        firstLease.Dispose();
        domain.Dispose();

        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OneOfTwoForegroundWaitersAcquiresTheReleasedMaintenancePermit(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease maintenance,
            out _);

        Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

        DomainOperationStatus firstStatus = DomainOperationStatus.DomainUnavailable;
        DomainOperationStatus secondStatus = DomainOperationStatus.DomainUnavailable;
        DomainOperationLease firstLease = default;
        DomainOperationLease secondLease = default;
        Thread first = new(() => firstStatus = Acquire(domain, out firstLease));
        Thread second = new(() => secondStatus = Acquire(domain, out secondLease));

        first.Start();
        second.Start();

        try
        {
            WaitForBlockedWait(first);
            WaitForBlockedWait(second);
        }
        finally
        {
            maintenance.Dispose();
        }

        Assert.IsTrue(first.Join(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(second.Join(TimeSpan.FromSeconds(5)));

        int acquiredCount =
            (firstStatus is DomainOperationStatus.Acquired ? 1 : 0) +
            (secondStatus is DomainOperationStatus.Acquired ? 1 : 0);
        int busyCount =
            (firstStatus is DomainOperationStatus.PermitBusy ? 1 : 0) +
            (secondStatus is DomainOperationStatus.PermitBusy ? 1 : 0);

        Assert.AreEqual(1, acquiredCount);
        Assert.AreEqual(1, busyCount);

        firstLease.Dispose();
        secondLease.Dispose();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OperationsAfterAMaintenanceWaitAllocateNoManagedMemory(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
            ExternalDomainReference.Maintenance,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease maintenance,
            out _);

        Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

        DomainOperationStatus attemptStatus = DomainOperationStatus.DomainUnavailable;
        DomainOperationLease attemptLease = default;
        Thread attempt = new(() => attemptStatus = Acquire(domain, out attemptLease));

        attempt.Start();

        try
        {
            WaitForBlockedWait(attempt);
        }
        finally
        {
            maintenance.Dispose();
        }

        Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(DomainOperationStatus.Acquired, attemptStatus);

        attemptLease.Dispose();

        long minimum = long.MaxValue;

        for (int i = 0; i < 10; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            DomainOperationStatus status = Acquire(domain, out DomainOperationLease lease);

            lease.Dispose();

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);

            Assert.AreEqual(DomainOperationStatus.Acquired, status);
        }

        Assert.AreEqual(0, minimum);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TheReleaseSignalIsReusedAcrossMaintenanceOperations(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        for (int i = 0; i < 2; i++)
        {
            DomainOperationStatus maintenanceStatus = domain.TryAcquireOperation(
                ExternalDomainReference.Maintenance,
                default,
                releaseExternalReferenceOnDispose: false,
                out DomainOperationLease maintenance,
                out _);

            Assert.AreEqual(DomainOperationStatus.Acquired, maintenanceStatus);

            DomainOperationStatus attemptStatus = DomainOperationStatus.DomainUnavailable;
            DomainOperationLease attemptLease = default;
            Thread attempt = new(() => attemptStatus = Acquire(domain, out attemptLease));

            attempt.Start();

            try
            {
                WaitForBlockedWait(attempt);
            }
            finally
            {
                maintenance.Dispose();
            }

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(DomainOperationStatus.Acquired, attemptStatus);

            attemptLease.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AFailedSchedulerReservationLeavesNoReferenceBehind(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        scheduler.ThrowOnEnter = true;

        DomainOperationStatus status = domain.TryAcquireOperation(
            ExternalDomainReference.TransientOperation,
            default,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease lease,
            out Exception? schedulerFailure);

        Assert.AreEqual(DomainOperationStatus.SchedulerBusy, status);
        Assert.IsFalse(lease.IsValid);
        Assert.IsInstanceOfType<InvalidOperationException>(schedulerFailure);
        Assert.AreEqual(0, scheduler.ExitCount);

        scheduler.ThrowOnEnter = false;

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease next));

        next.Dispose();

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AFailedSchedulerReservationConvergesAfterConcurrentDisposal(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();

        scheduler.OnEnter = () =>
        {
            entered.Set();
            release.Wait();
        };
        scheduler.ThrowOnEnter = true;

        DomainOperationStatus status = DomainOperationStatus.Acquired;
        Exception? schedulerFailure = null;
        Thread attempt = new(() =>
        {
            status = domain.TryAcquireOperation(
                ExternalDomainReference.TransientOperation,
                default,
                releaseExternalReferenceOnDispose: false,
                out _,
                out schedulerFailure);
        });

        attempt.Start();

        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));

            domain.Dispose();

            release.Set();

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            release.Set();

            if (attempt.IsAlive)
            {
                _ = attempt.Join(TimeSpan.FromSeconds(5));
            }
        }

        Assert.AreEqual(DomainOperationStatus.SchedulerBusy, status);
        Assert.IsInstanceOfType<InvalidOperationException>(schedulerFailure);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AFailedSchedulerReleasePoisonsBeforeWakingForegroundWork(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreEqual(
            DomainOperationStatus.Acquired,
            domain.TryAcquireOperation(
                ExternalDomainReference.Maintenance,
                default,
                releaseExternalReferenceOnDispose: false,
                out DomainOperationLease maintenance,
                out _));

        DomainOperationStatus status = DomainOperationStatus.Acquired;
        DomainOperationLease foreground = default;
        Thread attempt = new(() => status = Acquire(domain, out foreground));

        attempt.Start();
        WaitForBlockedWait(attempt);

        scheduler.ThrowOnExit = true;

        try
        {
            _ = Assert.ThrowsException<InvalidOperationException>(maintenance.Dispose);

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            scheduler.ThrowOnExit = false;
            foreground.Dispose();

            if (attempt.IsAlive)
            {
                attempt.Interrupt();
                _ = attempt.Join(TimeSpan.FromSeconds(5));
            }

            domain.Dispose();
        }

        Assert.AreEqual(DomainOperationStatus.DomainUnavailable, status);
        Assert.IsFalse(foreground.IsValid);
        Assert.IsNotNull(domain.PoisonReason);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DeviceTeardownWakesForegroundWorkWaitingForMaintenance(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreEqual(
            DomainOperationStatus.Acquired,
            domain.TryAcquireOperation(
                ExternalDomainReference.Maintenance,
                default,
                releaseExternalReferenceOnDispose: false,
                out DomainOperationLease maintenance,
                out _));

        DomainOperationStatus status = DomainOperationStatus.Acquired;
        DomainOperationLease foreground = default;
        Exception? failure = null;
        Thread attempt = new(() =>
        {
            try
            {
                status = Acquire(domain, out foreground);
            }
            catch (Exception e)
            {
                failure = e;
            }
        });

        attempt.Start();
        WaitForBlockedWait(attempt);

        try
        {
            domain.MarkDeviceTerminal(new InvalidOperationException("The device is terminal."));
            domain.ReleaseForDeviceTeardown();

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            try
            {
                maintenance.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            foreground.Dispose();

            if (attempt.IsAlive)
            {
                attempt.Interrupt();
                _ = attempt.Join(TimeSpan.FromSeconds(5));
            }
        }

        Assert.IsNull(failure);
        Assert.AreEqual(DomainOperationStatus.DomainUnavailable, status);
        Assert.IsFalse(foreground.IsValid);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ADisposedDomainRejectsNewOperations(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        domain.Dispose();

        Assert.AreEqual(DomainOperationStatus.DomainUnavailable, Acquire(domain, out DomainOperationLease lease));
        Assert.IsFalse(lease.IsValid);
        Assert.AreEqual(0, scheduler.EnterCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PoisoningADomainStartsItsTeardownAndRejectsNewOperations(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        InvalidOperationException reason = new("The provider can no longer be trusted.");

        domain.MarkPoisoned(reason);

        Assert.AreSame(reason, domain.PoisonReason);
        Assert.AreEqual(DomainOperationStatus.DomainUnavailable, Acquire(domain, out _));
        Assert.IsTrue(domain.IsDisposeRequested);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);

        domain.Dispose();

        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DisposingACopyOfAReleasedLeaseDoesNothing(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new FakeInteropProvider(graphicsDevice, scheduler));

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease lease));

        DomainOperationLease copy = lease;

        lease.Dispose();
        copy.Dispose();
        copy.Dispose();

        Assert.AreEqual(1, scheduler.ExitCount);

        Assert.AreEqual(DomainOperationStatus.Acquired, Acquire(domain, out DomainOperationLease next));

        next.Dispose();

        Assert.AreEqual(2, scheduler.ExitCount);
    }
}
