using System;
using System.Threading;
using ComputeSharp.Interop;

#pragma warning disable CA1063

namespace ComputeSharp;

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
}
