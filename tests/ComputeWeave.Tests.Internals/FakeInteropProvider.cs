using System;
using System.Threading;
using System.Threading.Tasks;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2213

namespace ComputeWeave.Tests.Internals;

/// <remarks>
/// The completion coordinator thread runs the release of a retired generation, so every counter a test reads
/// is written by a thread other than the one asserting on it and has to be published atomically.
/// </remarks>
internal sealed class FakeExternalView(FakeInteropProvider? provider = null) : IDisposable
{
    private int disposeCount;

    private int completedSignalsAtDispose;

    public int DisposeCount => Volatile.Read(ref this.disposeCount);

    public int CompletedSignalsAtDispose => Volatile.Read(ref this.completedSignalsAtDispose);

    public void Dispose()
    {
        Volatile.Write(ref this.completedSignalsAtDispose, provider?.CompletedSignalCount ?? 0);

        _ = Interlocked.Increment(ref this.disposeCount);
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

    public Action? OnEnter { get; set; }

    public bool ThrowOnExit { get; set; }

    /// <remarks>
    /// The completion coordinator thread runs the release of a retired generation, so a test thread observes
    /// these counters across threads and they have to be read and written atomically.
    /// </remarks>
    public bool IsReserved => EnterCount != ExitCount;

    protected override void EnterCore()
    {
        this.OnEnter?.Invoke();

        if (this.ThrowOnEnter)
        {
            throw new InvalidOperationException("External queue scheduler is busy or reentered.");
        }

        _ = Interlocked.Increment(ref this.enterCount);
    }

    protected override void ExitCore()
    {
        if (this.ThrowOnExit)
        {
            throw new InvalidOperationException("The external queue scheduler could not exit its reservation.");
        }

        _ = Interlocked.Increment(ref this.exitCount);
    }

    protected override void DisposeCore()
    {
        _ = Interlocked.Increment(ref this.disposeCount);
    }
}

/// <remarks>
/// An assertion this double makes can run on the completion coordinator thread, because that thread executes
/// the external drain. A drain that throws poisons its domain, and the coordinator contains the failures of a
/// poisoned domain rather than stopping, so such an assertion does not reach the test as its own message. It
/// surfaces as whatever the test observes afterwards instead. The library cannot reference the test framework,
/// so the containment cannot single assertions out. Keep the assertions here to conditions a passing test
/// never reaches, and read a confusing interop failure as a possible assertion from this class.
/// </remarks>
internal sealed unsafe class FakeInteropProvider(GraphicsDevice device, FakeInteropScheduler scheduler)
    : IComputeExternalInteropProvider<FakeExternalView>
{
    private readonly GraphicsDevice device = device;

    private readonly FakeInteropScheduler scheduler = scheduler;

    private ComPtr<ID3D12Fence> d3D12SharedFence;

    private Task? pendingSignal;

    private ManualResetEventSlim? signalGate;

    private ManualResetEventSlim? enqueueGate;

    private ManualResetEventSlim? enqueueEntered;

    private int completedSignalCount;

    public ExternalAdapterIdentity AdapterIdentity { get; set; } = new ExternalAdapterIdentity(device.Luid.ToInt64());

    // Persistent Lease を配るテストが多数あるため、既定でその順序保証も宣言する。宣言しない構成は
    // 仕様上 Persistent Lease を取れない。不足を試すテストは個別に減らして設定する。
    public ExternalInteropCapabilities Capabilities { get; set; } =
        ExternalInteropCapabilities.SharedFence |
        ExternalInteropCapabilities.SharedTexture2D |
        ExternalInteropCapabilities.SingleImmediateContextOrdering |
        ExternalInteropCapabilities.PersistentExternalViewOrdering;

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

    private int signalCount;

    private int flushCount;

    private int waitCount;

    private volatile bool wasReservedWhileSignaling;

    private volatile bool wasReservedWhileWaiting;

    /// <remarks>
    /// External maintenance can run on the completion coordinator thread, so a test asserting on these reads
    /// them from a thread other than the one that wrote them. Without atomic publication a test can observe a
    /// signal without the flush that follows it.
    /// </remarks>
    public int SignalCount => Volatile.Read(ref this.signalCount);

    public int FlushCount => Volatile.Read(ref this.flushCount);

    public int WaitCount => Volatile.Read(ref this.waitCount);

    public ulong ObservedSignalValue { get; private set; }

    public ulong ObservedWaitValue { get; private set; }

    public bool WasReservedWhileSignaling => this.wasReservedWhileSignaling;

    public bool WasReservedWhileWaiting => this.wasReservedWhileWaiting;

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

    /// <summary>
    /// Blocks the next signal inside the call, so a test can hold a maintenance pass inside its phase and
    /// observe what a pass running beside it does.
    /// </summary>
    public void BlockNextEnqueue()
    {
        this.enqueueEntered = new ManualResetEventSlim(false);
        this.enqueueGate = new ManualResetEventSlim(false);
    }

    /// <summary>
    /// Waits until a signal has entered the block set up by <see cref="BlockNextEnqueue"/>.
    /// </summary>
    /// <returns>Whether a signal entered the block.</returns>
    public bool WaitForBlockedEnqueue()
    {
        return this.enqueueEntered?.Wait(TimeSpan.FromSeconds(30)) is true;
    }

    /// <summary>
    /// Lets the blocked signal leave the call.
    /// </summary>
    public void ReleaseBlockedEnqueue()
    {
        this.enqueueGate?.Set();
    }

    /// <summary>
    /// Runs inside the signal, on the thread that is executing the drain phase.
    /// </summary>
    public Action? OnEnqueueSignal { get; set; }

    public void EnqueueSignal(ulong value)
    {
        ObservedSignalValue = value;
        this.wasReservedWhileSignaling = this.scheduler.IsReserved;

        _ = Interlocked.Increment(ref this.signalCount);

        this.OnEnqueueSignal?.Invoke();

        if (this.enqueueGate is ManualResetEventSlim blocked)
        {
            this.enqueueEntered!.Set();

            // The bound is a safety valve for a test that fails before it releases the block.
            Assert.IsTrue(blocked.Wait(TimeSpan.FromSeconds(30)), "A blocked enqueue was never released.");
        }

        if (this.ThrowOnSignal)
        {
            throw new InvalidOperationException("The external queue could not signal the shared timeline.");
        }

        if (this.signalGate is null && this.SignalDelayInMilliseconds <= 0)
        {
            // Counted before the fence advances. Advancing it first lets the drain observe the completion and
            // release the external view while this counter still reads the value from before the signal.
            _ = Interlocked.Increment(ref this.completedSignalCount);

            Assert.IsTrue(this.d3D12SharedFence.Get()->Signal(value) >= 0);

            return;
        }

        nint d3D12Fence = (nint)this.d3D12SharedFence.Get();
        int delay = this.SignalDelayInMilliseconds;
        ManualResetEventSlim? gate = this.signalGate;

        this.pendingSignal = Task.Run(() =>
        {
            if (gate is not null)
            {
                // The bound is a safety valve for a test that fails before it opens the gate. It never runs on
                // a passing path, where the gate decides the ordering.
                Assert.IsTrue(gate.Wait(TimeSpan.FromSeconds(30)), "A held signal was never released.");
            }
            else
            {
                Thread.Sleep(delay);
            }

            _ = Interlocked.Increment(ref this.completedSignalCount);

            Assert.IsTrue(((ID3D12Fence*)d3D12Fence)->Signal(value) >= 0);
        });
    }

    public void FlushAfterSignal()
    {
        _ = Interlocked.Increment(ref this.flushCount);
    }

    public bool ThrowOnWait { get; set; }

    public void EnqueueWait(ulong value)
    {
        ObservedWaitValue = value;
        this.wasReservedWhileWaiting = this.scheduler.IsReserved;

        _ = Interlocked.Increment(ref this.waitCount);

        if (this.ThrowOnWait)
        {
            throw new InvalidOperationException("The external queue could not wait on the shared timeline.");
        }
    }

    private int openSharedTextureCount;

    private volatile FakeExternalView? lastOpenedView;

    public int OpenSharedTextureCount => Volatile.Read(ref this.openSharedTextureCount);

    public bool WasReservedWhileOpeningTexture { get; private set; }

    public nint ObservedTextureHandle { get; private set; }

    public ExternalTextureDescriptor ObservedTextureDescriptor { get; private set; }

    public FakeExternalView? LastOpenedView => this.lastOpenedView;

    public FakeExternalView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        WasReservedWhileOpeningTexture = this.scheduler.IsReserved;
        ObservedTextureHandle = resourceHandle.DangerousGetHandle();
        ObservedTextureDescriptor = descriptor;

        using ComPtr<ID3D12Resource> d3D12Resource = this.device.OpenSharedResource(new HANDLE((void*)ObservedTextureHandle));

        if (d3D12Resource.Get() is null)
        {
            throw new InvalidOperationException("The shared texture could not be opened.");
        }

        FakeExternalView view = new(this);

        this.lastOpenedView = view;

        _ = Interlocked.Increment(ref this.openSharedTextureCount);

        return view;
    }

    public void OnDeviceTerminal(Exception reason)
    {
    }

    public void Dispose()
    {
        DisposeCount++;

        this.signalGate?.Set();
        this.enqueueGate?.Set();
        this.pendingSignal?.Wait();
        this.signalGate?.Dispose();
        this.enqueueGate?.Dispose();
        this.enqueueEntered?.Dispose();
        this.d3D12SharedFence.Dispose();
    }
}
