using System;
using System.Threading;
using ComputeWeave.Win32;

namespace ComputeWeave.Graphics.Pipelines;

internal sealed unsafe class CompletionCoordinator : IDisposable
{
    private readonly GraphicsDevice device;

    private readonly CompletionRegistry registry;

    private readonly Thread thread;

    private readonly HANDLE eventHandle;

    private readonly object progressGate = new();

    private ulong progressVersion;

    private volatile bool isDisposed;

    private volatile Exception? failure;

    private volatile DeviceRegistrationRegistry? registrations;

    public CompletionCoordinator(GraphicsDevice device, CompletionRegistry registry)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(registry);

        this.device = device;
        this.registry = registry;
        this.eventHandle = device.CreateCompletionCoordinatorEvent();

        this.thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ComputeWeave completion coordinator"
        };

        this.thread.Start();
    }

    public Exception? Failure => this.failure;

    public HANDLE EventHandle => this.eventHandle;

    public ulong ProgressVersion
    {
        get
        {
            lock (this.progressGate)
            {
                return this.progressVersion;
            }
        }
    }

    public bool TryWaitForProgress(ulong observedVersion)
    {
        lock (this.progressGate)
        {
            while (this.progressVersion == observedVersion && !this.isDisposed && this.failure is null)
            {
                _ = Monitor.Wait(this.progressGate);
            }

            return this.progressVersion != observedVersion;
        }
    }

    public void AttachRegistrations(DeviceRegistrationRegistry registrations)
    {
        default(ArgumentNullException).ThrowIfNull(registrations);
        default(InvalidOperationException).ThrowIf(this.registrations is not null, "The completion coordinator already has a registration registry.");

        this.registrations = registrations;
    }

    public void Wake()
    {
        if (this.isDisposed)
        {
            return;
        }

        _ = Windows.SetEvent(this.eventHandle);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;

        _ = Windows.SetEvent(this.eventHandle);

        this.thread.Join();
    }

    private void Run()
    {
        try
        {
            while (!this.isDisposed)
            {
                try
                {
                    if (TryGetArmedFence(out ulong fenceValue))
                    {
                        this.device.ArmComputeFenceEvent(fenceValue, this.eventHandle);
                    }
                }
                catch (Exception e)
                {
                    this.failure = e;

                    return;
                }

                _ = Windows.WaitForSingleObjectEx(this.eventHandle, Windows.INFINITE, Windows.FALSE);

                if (this.isDisposed)
                {
                    return;
                }

                try
                {
                    while (ComputeSubmissionExecutor.TryReleaseCompleted(this.device, this.registry))
                    {
                    }

                    this.registrations?.RunExternalMaintenance();
                }
                catch (Exception e)
                {
                    this.failure = e;

                    return;
                }

                PublishProgress();
            }
        }
        finally
        {
            WakeWaiters();
        }
    }

    private bool TryGetArmedFence(out ulong fenceValue)
    {
        bool hasFence = this.registry.TryGetMinimumCommittedFence(out fenceValue);

        if (this.registrations?.TryGetMinimumDrainFence(out ulong drainFenceValue) is true &&
            (!hasFence || drainFenceValue < fenceValue))
        {
            fenceValue = drainFenceValue;
            hasFence = true;
        }

        return hasFence;
    }

    private void PublishProgress()
    {
        lock (this.progressGate)
        {
            this.progressVersion++;

            Monitor.PulseAll(this.progressGate);
        }
    }

    private void WakeWaiters()
    {
        lock (this.progressGate)
        {
            Monitor.PulseAll(this.progressGate);
        }
    }
}
