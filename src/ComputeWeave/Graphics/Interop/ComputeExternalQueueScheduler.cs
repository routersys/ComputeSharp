using System;
using System.Threading;
using ComputeWeave.Interop;

#pragma warning disable CA1063

namespace ComputeWeave;

/// <summary>
/// A scheduler serializing the external queue work of the immediate context an interop provider enqueues onto.
/// </summary>
/// <remarks>
/// One scheduler corresponds to exactly one immediate context. Providers sharing that context return the same
/// scheduler instance, so that every domain built on it enqueues its shared resource work in a single order.
/// </remarks>
public abstract class ComputeExternalQueueScheduler : IDisposable
{
    /// <summary>
    /// The exclusion protecting <see cref="counts"/> and <see cref="isDisposeCoreInvoked"/>.
    /// </summary>
    private SpinLock exclusion;

    /// <summary>
    /// The outstanding references keeping the current scheduler alive.
    /// </summary>
    private SchedulerReferenceCounts counts;

    /// <summary>
    /// Whether <see cref="DisposeCore"/> has been claimed by a thread.
    /// </summary>
    private bool isDisposeCoreInvoked;

    /// <summary>
    /// Creates a new <see cref="ComputeExternalQueueScheduler"/> instance.
    /// </summary>
    protected ComputeExternalQueueScheduler()
    {
        this.counts = new SchedulerReferenceCounts();
    }

    /// <summary>
    /// Creates a scheduler admitting one reservation of an external queue at a time.
    /// </summary>
    /// <returns>The created <see cref="ComputeExternalQueueScheduler"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// The returned scheduler rejects a reservation taken while another one is held, including one taken again
    /// on the thread already holding it. It owns nothing beyond that, so releasing it releases no queue, no
    /// context and no device.
    /// </para>
    /// <para>
    /// One scheduler corresponds to exactly one immediate context. The caller creates one per immediate context
    /// and returns that same instance from every provider enqueueing onto it. This call hands out a new instance
    /// every time and does not know which context a scheduler was created for, so the caller keeps that mapping.
    /// </para>
    /// <para>
    /// The caller owns the returned scheduler. A provider must not release it, and the caller releases it after
    /// the domains built on it. Releasing it earlier only rejects new domain registrations.
    /// </para>
    /// </remarks>
    public static ComputeExternalQueueScheduler Create()
    {
        return new SingleReservationScheduler();
    }

    /// <summary>
    /// Enters the exclusive reservation of the external queue.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the external queue is already reserved.</exception>
    protected abstract void EnterCore();

    /// <summary>
    /// Exits the exclusive reservation of the external queue.
    /// </summary>
    protected abstract void ExitCore();

    /// <summary>
    /// Releases the resources held by the current scheduler.
    /// </summary>
    /// <remarks>
    /// This runs exactly once, after the owner reference has been released, every domain registration has been
    /// released, and no reservation is active.
    /// </remarks>
    protected abstract void DisposeCore();

    /// <summary>
    /// Releases the owner reference of the current scheduler.
    /// </summary>
    /// <remarks>
    /// Releasing the owner reference only rejects new domain registrations. The operations of the registrations
    /// that are already held keep running, and the scheduler stays alive until the last one is released.
    /// </remarks>
    public void Dispose()
    {
        bool isDisposingCore;
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            isDisposingCore = this.counts.TryReleaseOwner() && TryClaimDisposeCore();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }

        if (isDisposingCore)
        {
            DisposeCore();
        }
    }

    /// <summary>
    /// Tries to acquire a domain registration reference over the current scheduler.
    /// </summary>
    /// <returns>Whether the registration reference was acquired.</returns>
    internal bool TryAcquireRegistration()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            return this.counts.TryAcquireRegistration();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }
    }

    /// <summary>
    /// Releases a domain registration reference over the current scheduler.
    /// </summary>
    internal void ReleaseRegistration()
    {
        bool isDisposingCore;
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            this.counts.ReleaseRegistration();

            isDisposingCore = TryClaimDisposeCore();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }

        if (isDisposingCore)
        {
            DisposeCore();
        }
    }

    /// <summary>
    /// Enters an exclusive reservation of the external queue of the current scheduler.
    /// </summary>
    internal void EnterReservation()
    {
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            this.counts.AcquireReservation();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }

        try
        {
            EnterCore();
        }
        catch
        {
            ReleaseReservation();

            throw;
        }
    }

    /// <summary>
    /// Exits the exclusive reservation of the external queue of the current scheduler.
    /// </summary>
    internal void ExitReservation()
    {
        try
        {
            ExitCore();
        }
        finally
        {
            ReleaseReservation();
        }
    }

    /// <summary>
    /// Releases the reservation reference of the current scheduler.
    /// </summary>
    private void ReleaseReservation()
    {
        bool isDisposingCore;
        bool taken = false;

        try
        {
            this.exclusion.Enter(ref taken);

            this.counts.ReleaseReservation();

            isDisposingCore = TryClaimDisposeCore();
        }
        finally
        {
            if (taken)
            {
                this.exclusion.Exit(useMemoryBarrier: true);
            }
        }

        if (isDisposingCore)
        {
            DisposeCore();
        }
    }

    /// <summary>
    /// Claims the single <see cref="DisposeCore"/> call, if the current scheduler holds no reference anymore.
    /// </summary>
    /// <returns>Whether the calling thread claimed the call.</returns>
    private bool TryClaimDisposeCore()
    {
        if (this.isDisposeCoreInvoked || !this.counts.IsReleased)
        {
            return false;
        }

        this.isDisposeCoreInvoked = true;

        return true;
    }

    /// <summary>
    /// A <see cref="ComputeExternalQueueScheduler"/> admitting one reservation of its external queue at a time.
    /// </summary>
    private sealed class SingleReservationScheduler : ComputeExternalQueueScheduler
    {
        /// <summary>
        /// Whether the external queue is reserved.
        /// </summary>
        private int isReserved;

        /// <inheritdoc/>
        protected override void EnterCore()
        {
            default(ComputeDiagnosticException).ThrowIf(
                Interlocked.CompareExchange(ref this.isReserved, 1, 0) != 0,
                ComputeDiagnosticIds.SchedulerBusy,
                "External queue scheduler is busy or reentered.");
        }

        /// <inheritdoc/>
        protected override void ExitCore()
        {
            default(ComputeDiagnosticException).ThrowIf(
                Interlocked.Exchange(ref this.isReserved, 0) != 1,
                ComputeDiagnosticIds.SchedulerContract,
                "Scheduler exit invariant failed.");
        }

        /// <inheritdoc/>
        protected override void DisposeCore()
        {
        }
    }
}
