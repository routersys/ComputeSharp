using System;
using System.Collections.Generic;
using System.Threading;
using ComputeWeave.Interop;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ExternalViewLeaseTests
{
    private sealed class Fixture(GraphicsDevice device, ComputeSharedTextureInitialOwner initialOwner) : IDisposable
    {
        private readonly List<ExternalTextureLease<FakeExternalView>> leases = [];

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

        public ref ResourceGenerationRecord Record => ref GetOwner().GetResourceRecord(0);

        public ExternalTextureLease<FakeExternalView> AcquireLease()
        {
            ExternalTextureLease<FakeExternalView> lease = Slot.AcquireExternalViewLease();

            this.leases.Add(lease);

            return lease;
        }

        public ResourceGenerationOwner GetOwner()
        {
            ReadWriteTexture2D<Bgra32, Float4> texture = Slot.GetComputeBinding().Resource!;

            Assert.IsTrue(((IGenerationBoundResource)texture).TryGetGenerationBinding(out ResourceUsageBinding binding));

            return (ResourceGenerationOwner)binding.Set.Owner;
        }

        public void Dispose()
        {
            foreach (ExternalTextureLease<FakeExternalView> lease in this.leases)
            {
                lease.Dispose();
            }

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
    public void BorrowsTheExternalViewOfThePublishedGeneration(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;
        int reservations = fixture.Scheduler.EnterCount;

        using (BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            Assert.IsTrue(borrow.IsValid);
            Assert.AreSame(view, borrow.DangerousGetView());
            Assert.AreEqual(reservations + 1, fixture.Scheduler.EnterCount);
            Assert.IsTrue(fixture.Scheduler.IsReserved);
            Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);
        }

        Assert.AreEqual(fixture.Scheduler.EnterCount, fixture.Scheduler.ExitCount);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);
        Assert.AreEqual(0, view.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesABorrowedExternalViewOnlyOnce(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Slot.BeginExternalOperation();
        BorrowedExternalTextureView<FakeExternalView> copy = borrow;

        Assert.IsTrue(copy.IsValid);

        borrow.Dispose();
        copy.Dispose();
        copy.Dispose();

        Assert.IsFalse(copy.IsValid);
        Assert.AreEqual(fixture.Scheduler.EnterCount, fixture.Scheduler.ExitCount);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);

        try
        {
            _ = copy.DangerousGetView();

            Assert.Fail("A released borrow still handed out its external view.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsABorrowOfAGenerationTheComputeQueueOwns(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        int reservations = fixture.Scheduler.EnterCount;

        ComputeDiagnosticException rejection = Assert.ThrowsException<ComputeDiagnosticException>(() => { _ = fixture.Slot.BeginExternalOperation().IsValid; });

        Assert.AreEqual("CMPW4002", rejection.DiagnosticId);

        Assert.AreEqual(reservations + 1, fixture.Scheduler.EnterCount);
        Assert.AreEqual(fixture.Scheduler.EnterCount, fixture.Scheduler.ExitCount);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsASecondBorrowWhileOneHoldsTheExternalQueue(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        using (BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            ComputeDiagnosticException rejection = Assert.ThrowsException<ComputeDiagnosticException>(() => { _ = fixture.Slot.BeginExternalOperation().IsValid; });

            Assert.AreEqual("CMPW3004", rejection.DiagnosticId);

            Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);
        }

        Assert.AreEqual(fixture.Scheduler.EnterCount, fixture.Scheduler.ExitCount);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void HoldsTheExternalViewOfARetiredGenerationUntilItsBorrowIsReleased(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;

        using (BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Slot.BeginExternalOperation())
        {
            fixture.Slot.Dispose();

            Assert.AreEqual(0, fixture.Provider.SignalCount);
            Assert.AreEqual(0, view.DisposeCount);
        }

        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, fixture.Provider.SignalCount, "signal count");
        Assert.AreEqual(1, view.DisposeCount, "view dispose count");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnInterruptedBorrowWaitReleasesItsGenerationReference(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));
        Assert.AreEqual(
            DomainOperationStatus.Acquired,
            fixture.Domain.TryAcquireOperation(
                ExternalDomainReference.Maintenance,
                default,
                releaseExternalReferenceOnDispose: false,
                out DomainOperationLease maintenance,
                out _));

        Exception? failure = null;
        Thread attempt = new(() =>
        {
            try
            {
                using BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Slot.BeginExternalOperation();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });

        attempt.Start();

        try
        {
            Assert.IsTrue(SpinWait.SpinUntil(
                () => (attempt.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);

            attempt.Interrupt();

            Assert.IsTrue(attempt.Join(TimeSpan.FromSeconds(5)));
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

        Assert.IsInstanceOfType<ThreadInterruptedException>(failure);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LeasesTheExternalViewOfThePublishedGeneration(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;
        int reservations = fixture.Scheduler.EnterCount;

        ExternalTextureLease<FakeExternalView> lease = fixture.AcquireLease();

        Assert.IsFalse(lease.IsDisposed);
        Assert.AreEqual(16, lease.Width);
        Assert.AreEqual(16, lease.Height);
        Assert.AreSame(view, lease.DangerousGetView());
        Assert.AreEqual(reservations, fixture.Scheduler.EnterCount);
        Assert.AreNotEqual(0, fixture.Record.StateFlags & ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.PersistentLeaseActiveBit);
        Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);

        lease.Dispose();
        lease.Dispose();

        Assert.IsTrue(lease.IsDisposed);
        Assert.AreEqual(0, fixture.Record.StateFlags & ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.PersistentLeaseActiveBit);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);

        _ = Assert.ThrowsException<ObjectDisposedException>(() => _ = lease.Width);
        _ = Assert.ThrowsException<ObjectDisposedException>(() => _ = lease.Height);
        _ = Assert.ThrowsException<ObjectDisposedException>(lease.DangerousGetView);
        _ = Assert.ThrowsException<ObjectDisposedException>(() => { _ = lease.BeginExternalQueueOperation().IsValid; });
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsASecondLeaseOfTheSameGeneration(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        _ = fixture.AcquireLease();

        _ = Assert.ThrowsException<InvalidOperationException>(fixture.Slot.AcquireExternalViewLease);

        Assert.AreNotEqual(0, fixture.Record.StateFlags & ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.PersistentLeaseActiveBit);
        Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void GetsTheLeasedDimensionsWithoutManagedAllocation(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 8, out _));

        ExternalTextureLease<FakeExternalView> lease = fixture.AcquireLease();
        long minimum = long.MaxValue;
        int width = 0;
        int height = 0;

        for (int i = 0; i < 10; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int j = 0; j < 1000; j++)
            {
                width = lease.Width;
                height = lease.Height;
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.AreEqual(16, width);
        Assert.AreEqual(8, height);
        Assert.AreEqual(0, minimum);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsALeaseOfAGenerationTheComputeQueueOwns(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        _ = Assert.ThrowsException<InvalidOperationException>(fixture.Slot.AcquireExternalViewLease);

        Assert.AreEqual(0, fixture.Record.StateFlags & ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.PersistentLeaseActiveBit);
        Assert.AreEqual(0, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LeasesTwoGenerationsWhileASlotReplacesOne(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView first = fixture.Provider.LastOpenedView!;
        ExternalTextureLease<FakeExternalView> firstLease = fixture.AcquireLease();

        Assert.IsTrue(fixture.Slot.TryEnsure(32, 16, out bool changed));
        Assert.IsTrue(changed);

        FakeExternalView second = fixture.Provider.LastOpenedView!;
        ExternalTextureLease<FakeExternalView> secondLease = fixture.AcquireLease();

        Assert.AreNotSame(first, second);
        Assert.AreEqual(16, firstLease.Width);
        Assert.AreEqual(16, firstLease.Height);
        Assert.AreEqual(32, secondLease.Width);
        Assert.AreEqual(16, secondLease.Height);
        Assert.AreSame(first, firstLease.DangerousGetView());
        Assert.AreSame(second, secondLease.DangerousGetView());
        Assert.AreEqual(0, fixture.Provider.SignalCount);
        Assert.AreEqual(0, first.DisposeCount);

        _ = Assert.ThrowsException<InvalidOperationException>(fixture.Slot.AcquireExternalViewLease);

        firstLease.Dispose();
        secondLease.Dispose();

        fixture.Slot.Dispose();
        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReservesTheExternalQueueForTheOperationOfALease(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        ExternalTextureLease<FakeExternalView> lease = fixture.AcquireLease();
        int reservations = fixture.Scheduler.EnterCount;

        using (ExternalQueueOperation operation = lease.BeginExternalQueueOperation())
        {
            Assert.IsTrue(operation.IsValid);
            Assert.AreEqual(reservations + 1, fixture.Scheduler.EnterCount);
            Assert.IsTrue(fixture.Scheduler.IsReserved);

            // 既に操作が活きている間の二つ目は CMPW3004 で拒まれる。
            ComputeDiagnosticException rejection = Assert.ThrowsExactly<ComputeDiagnosticException>(
                () => _ = lease.BeginExternalQueueOperation().IsValid);

            Assert.AreEqual("CMPW3004", rejection.DiagnosticId);
            Assert.IsInstanceOfType<InvalidOperationException>(rejection);
        }

        Assert.AreEqual(fixture.Scheduler.EnterCount, fixture.Scheduler.ExitCount);
        Assert.AreEqual(1, fixture.Record.ExternalReferenceCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void HoldsTheDrainOfARetiredGenerationUntilItsLeaseIsDisposed(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;
        ExternalTextureLease<FakeExternalView> lease = fixture.AcquireLease();

        try
        {
            fixture.Slot.Dispose();

            Assert.AreEqual(0, fixture.Provider.SignalCount);
            Assert.AreEqual(0, view.DisposeCount);
        }
        finally
        {
            lease.Dispose();
        }

        fixture.Slot.WaitForDisposal();

        Assert.AreEqual(1, fixture.Provider.SignalCount);
        Assert.AreEqual(1, view.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheExternalViewOfALeaseWhenItsDomainIsPoisoned(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.IsTrue(fixture.Slot.TryEnsure(16, 16, out _));

        FakeExternalView view = fixture.Provider.LastOpenedView!;
        ResourceGenerationOwner owner = fixture.GetOwner();
        ExternalTextureLease<FakeExternalView> lease = fixture.AcquireLease();

        InvalidOperationException reason = new("The provider can no longer be trusted.");

        fixture.Domain.MarkPoisoned(reason);

        // A poisoned domain converges through the coordinator like every other external release.
        ExternalMaintenanceWait.WaitFor(
            device.Get(),
            () => view.DisposeCount == 1,
            "the external view of the poisoned domain was released");

        Assert.AreEqual(1, view.DisposeCount);
        Assert.AreSame(reason, Assert.ThrowsException<InvalidOperationException>(lease.DangerousGetView));
        Assert.AreSame(
            reason,
            Assert.ThrowsException<InvalidOperationException>(() => { _ = lease.BeginExternalQueueOperation().IsValid; }));

        lease.Dispose();

        Assert.AreEqual(0, owner.GetResourceRecord(0).ExternalReferenceCount);
        Assert.AreEqual(0, owner.GetResourceRecord(0).StateFlags & ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.PersistentLeaseActiveBit);
    }
}
