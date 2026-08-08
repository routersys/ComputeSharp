using System;
using System.Threading;
using System.Threading.Tasks;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2213

namespace ComputeWeave.Tests.Internals;

internal sealed class FakeExternalView(FakeInteropProvider? provider = null) : IDisposable
{
    public int DisposeCount { get; private set; }

    public int CompletedSignalsAtDispose { get; private set; }

    public void Dispose()
    {
        DisposeCount++;
        CompletedSignalsAtDispose = provider?.CompletedSignalCount ?? 0;
    }
}

internal sealed class FakeInteropScheduler : ComputeExternalQueueScheduler
{
    private int enterCount;

    private int exitCount;

    private int disposeCount;

    public int EnterCount => Volatile.Read(ref this.enterCount);

    public int ExitCount => Volatile.Read(ref this.exitCount);

    public int DisposeCount => Volatile.Read(ref this.disposeCount);

    public bool ThrowOnEnter { get; set; }

    /// <remarks>
    /// The completion coordinator thread runs the release of a retired generation, so a test thread observes
    /// these counters across threads and they have to be read and written atomically.
    /// </remarks>
    public bool IsReserved => EnterCount != ExitCount;

    protected override void EnterCore()
    {
        if (this.ThrowOnEnter)
        {
            throw new InvalidOperationException("External queue scheduler is busy or reentered.");
        }

        _ = Interlocked.Increment(ref this.enterCount);
    }

    protected override void ExitCore()
    {
        _ = Interlocked.Increment(ref this.exitCount);
    }

    protected override void DisposeCore()
    {
        _ = Interlocked.Increment(ref this.disposeCount);
    }
}

internal sealed unsafe class FakeInteropProvider(GraphicsDevice device, FakeInteropScheduler scheduler)
    : IComputeExternalInteropProvider<FakeExternalView>
{
    private readonly GraphicsDevice device = device;

    private readonly FakeInteropScheduler scheduler = scheduler;

    private ComPtr<ID3D12Fence> d3D12SharedFence;

    private Task? pendingSignal;

    private ManualResetEventSlim? signalGate;

    private int completedSignalCount;

    public ExternalAdapterIdentity AdapterIdentity { get; set; } = new ExternalAdapterIdentity(device.Luid.ToInt64());

    public ExternalInteropCapabilities Capabilities { get; set; } =
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering;

    public ComputeExternalQueueScheduler Scheduler => this.scheduler;

    public bool ThrowOnInitialize { get; set; }

    public int InitializeCount { get; private set; }

    public int DisposeCount { get; private set; }

    public nint ObservedFenceHandle { get; private set; }

    public bool OpenedSharedFence { get; private set; }

    public void Initialize(in ExternalTimelineInitialization initialization)
    {
        InitializeCount++;
        ObservedFenceHandle = initialization.SharedFenceHandle.DangerousGetHandle();

        this.d3D12SharedFence = this.device.OpenSharedFence(new HANDLE((void*)ObservedFenceHandle));

        OpenedSharedFence = this.d3D12SharedFence.Get() is not null;

        if (this.ThrowOnInitialize)
        {
            throw new InvalidOperationException("The provider could not open the shared timeline.");
        }
    }

    public int SignalCount { get; private set; }

    public int FlushCount { get; private set; }

    public int WaitCount { get; private set; }

    public ulong ObservedSignalValue { get; private set; }

    public ulong ObservedWaitValue { get; private set; }

    public bool WasReservedWhileSignaling { get; private set; }

    public bool WasReservedWhileWaiting { get; private set; }

    public bool ThrowOnSignal { get; set; }

    public int SignalDelayInMilliseconds { get; set; }

    public int CompletedSignalCount => Volatile.Read(ref this.completedSignalCount);

    /// <summary>
    /// Holds every subsequent signal until <see cref="ReleaseHeldSignals"/> runs, so a test can observe the
    /// state of a drain that cannot complete yet without depending on wall clock time.
    /// </summary>
    public void HoldSignals()
    {
        this.signalGate = new ManualResetEventSlim(false);
    }

    /// <summary>
    /// Lets every signal held by <see cref="HoldSignals"/> reach the shared timeline.
    /// </summary>
    public void ReleaseHeldSignals()
    {
        this.signalGate?.Set();
    }

    public void EnqueueSignal(ulong value)
    {
        SignalCount++;
        ObservedSignalValue = value;
        WasReservedWhileSignaling = this.scheduler.IsReserved;

        if (this.ThrowOnSignal)
        {
            throw new InvalidOperationException("The external queue could not signal the shared timeline.");
        }

        if (this.signalGate is null && this.SignalDelayInMilliseconds <= 0)
        {
            Assert.IsTrue(this.d3D12SharedFence.Get()->Signal(value) >= 0);

            _ = Interlocked.Increment(ref this.completedSignalCount);

            return;
        }

        nint d3D12Fence = (nint)this.d3D12SharedFence.Get();
        int delay = this.SignalDelayInMilliseconds;
        ManualResetEventSlim? gate = this.signalGate;

        this.pendingSignal = Task.Run(() =>
        {
            if (gate is not null)
            {
                gate.Wait();
            }
            else
            {
                Thread.Sleep(delay);
            }

            Assert.IsTrue(((ID3D12Fence*)d3D12Fence)->Signal(value) >= 0);

            _ = Interlocked.Increment(ref this.completedSignalCount);
        });
    }

    public void FlushAfterSignal()
    {
        FlushCount++;
    }

    public bool ThrowOnWait { get; set; }

    public void EnqueueWait(ulong value)
    {
        WaitCount++;
        ObservedWaitValue = value;
        WasReservedWhileWaiting = this.scheduler.IsReserved;

        if (this.ThrowOnWait)
        {
            throw new InvalidOperationException("The external queue could not wait on the shared timeline.");
        }
    }

    public int OpenSharedTextureCount { get; private set; }

    public bool WasReservedWhileOpeningTexture { get; private set; }

    public nint ObservedTextureHandle { get; private set; }

    public ExternalTextureDescriptor ObservedTextureDescriptor { get; private set; }

    public FakeExternalView? LastOpenedView { get; private set; }

    public FakeExternalView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        OpenSharedTextureCount++;
        WasReservedWhileOpeningTexture = this.scheduler.IsReserved;
        ObservedTextureHandle = resourceHandle.DangerousGetHandle();
        ObservedTextureDescriptor = descriptor;

        using ComPtr<ID3D12Resource> d3D12Resource = this.device.OpenSharedResource(new HANDLE((void*)ObservedTextureHandle));

        if (d3D12Resource.Get() is null)
        {
            throw new InvalidOperationException("The shared texture could not be opened.");
        }

        LastOpenedView = new FakeExternalView(this);

        return LastOpenedView;
    }

    public void OnDeviceTerminal(Exception reason)
    {
    }

    public void Dispose()
    {
        DisposeCount++;

        this.signalGate?.Set();
        this.pendingSignal?.Wait();
        this.signalGate?.Dispose();
        this.d3D12SharedFence.Dispose();
    }
}
