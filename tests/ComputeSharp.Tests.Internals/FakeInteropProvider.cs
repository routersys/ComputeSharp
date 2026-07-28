using System;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2213

namespace ComputeSharp.Tests.Internals;

internal sealed class FakeExternalView : IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose()
    {
        DisposeCount++;
    }
}

internal sealed class FakeInteropScheduler : ComputeExternalQueueScheduler
{
    public int EnterCount { get; private set; }

    public int ExitCount { get; private set; }

    public int DisposeCount { get; private set; }

    public bool ThrowOnEnter { get; set; }

    public bool IsReserved => EnterCount != ExitCount;

    protected override void EnterCore()
    {
        if (this.ThrowOnEnter)
        {
            throw new InvalidOperationException("External queue scheduler is busy or reentered.");
        }

        EnterCount++;
    }

    protected override void ExitCore()
    {
        ExitCount++;
    }

    protected override void DisposeCore()
    {
        DisposeCount++;
    }
}

internal sealed unsafe class FakeInteropProvider(GraphicsDevice device, FakeInteropScheduler scheduler)
    : IComputeExternalInteropProvider<FakeExternalView>
{
    private readonly GraphicsDevice device = device;

    private readonly FakeInteropScheduler scheduler = scheduler;

    private ComPtr<ID3D12Fence> d3D12SharedFence;

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

    public void EnqueueSignal(ulong value)
    {
        SignalCount++;
        ObservedSignalValue = value;
        WasReservedWhileSignaling = this.scheduler.IsReserved;

        if (this.ThrowOnSignal)
        {
            throw new InvalidOperationException("The external queue could not signal the shared timeline.");
        }

        Assert.IsTrue(this.d3D12SharedFence.Get()->Signal(value) >= 0);
    }

    public void FlushAfterSignal()
    {
        FlushCount++;
    }

    public void EnqueueWait(ulong value)
    {
        WaitCount++;
        ObservedWaitValue = value;
        WasReservedWhileWaiting = this.scheduler.IsReserved;
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

        LastOpenedView = new FakeExternalView();

        return LastOpenedView;
    }

    public void OnDeviceTerminal(Exception reason)
    {
    }

    public void Dispose()
    {
        DisposeCount++;

        this.d3D12SharedFence.Dispose();
    }
}
