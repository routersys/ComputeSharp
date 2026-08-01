using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

#pragma warning disable IDE0290

namespace ComputeWeave.Interop;

internal sealed class SchedulerRegistration
{
    private readonly ComputeExternalQueueScheduler scheduler;

    private int isReleased;

    private SchedulerRegistration(ComputeExternalQueueScheduler scheduler)
    {
        this.scheduler = scheduler;
    }

    public ComputeExternalQueueScheduler Scheduler => this.scheduler;

    public static bool TryAcquire(
        ComputeExternalQueueScheduler scheduler,
        [NotNullWhen(true)] out SchedulerRegistration? registration)
    {
        default(ArgumentNullException).ThrowIfNull(scheduler);

        if (!scheduler.TryAcquireRegistration())
        {
            registration = null;

            return false;
        }

        registration = new SchedulerRegistration(scheduler);

        return true;
    }

    public void EnterReservation()
    {
        default(InvalidOperationException).ThrowIf(
            Volatile.Read(ref this.isReleased) != 0,
            "The scheduler registration has been released.");

        this.scheduler.EnterReservation();
    }

    public void ExitReservation()
    {
        this.scheduler.ExitReservation();
    }

    public void Release()
    {
        if (Interlocked.Exchange(ref this.isReleased, 1) == 0)
        {
            this.scheduler.ReleaseRegistration();
        }
    }
}
