using System;
using System.Threading;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

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
            Name = "ComputeSharp completion coordinator"
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
                    if (this.registry.TryGetMinimumCommittedFence(out ulong fenceValue))
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
