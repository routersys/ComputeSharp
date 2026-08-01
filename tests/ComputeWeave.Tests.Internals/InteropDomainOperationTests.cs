using System;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class InteropDomainOperationTests
{
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
