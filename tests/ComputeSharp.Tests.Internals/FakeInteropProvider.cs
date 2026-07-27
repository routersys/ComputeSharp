using System;
using ComputeSharp.Win32;

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

        using ComPtr<ID3D12Fence> d3D12Fence = this.device.OpenSharedFence(new HANDLE((void*)ObservedFenceHandle));

        OpenedSharedFence = d3D12Fence.Get() is not null;

        if (this.ThrowOnInitialize)
        {
            throw new InvalidOperationException("The provider could not open the shared timeline.");
        }
    }

    public void EnqueueSignal(ulong value)
    {
    }

    public void FlushAfterSignal()
    {
    }

    public void EnqueueWait(ulong value)
    {
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
    }
}
