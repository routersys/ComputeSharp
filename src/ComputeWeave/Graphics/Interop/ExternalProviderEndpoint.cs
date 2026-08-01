using System;
using System.Threading;

namespace ComputeWeave.Interop;

internal abstract class ExternalProviderEndpoint
{
    private int isDeviceTerminalNotified;

    private int isProviderDisposed;

    public abstract ExternalAdapterIdentity AdapterIdentity { get; }

    public abstract ComputeExternalQueueScheduler Scheduler { get; }

    public abstract ExternalInteropCapabilities Capabilities { get; }

    public abstract void Initialize(in ExternalTimelineInitialization initialization);

    public abstract void EnqueueSignal(ulong value);

    public abstract void FlushAfterSignal();

    public abstract void EnqueueWait(ulong value);

    public Exception? NotifyDeviceTerminal(Exception reason)
    {
        default(ArgumentNullException).ThrowIfNull(reason);

        if (Interlocked.Exchange(ref this.isDeviceTerminalNotified, 1) != 0)
        {
            return null;
        }

        try
        {
            OnDeviceTerminalCore(reason);

            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public void DisposeProvider()
    {
        if (Interlocked.Exchange(ref this.isProviderDisposed, 1) == 0)
        {
            DisposeProviderCore();
        }
    }

    protected abstract void OnDeviceTerminalCore(Exception reason);

    protected abstract void DisposeProviderCore();
}

internal sealed class ExternalProviderEndpoint<TView> : ExternalProviderEndpoint
    where TView : class, IDisposable
{
    private readonly IComputeExternalInteropProvider<TView> provider;

    public ExternalProviderEndpoint(IComputeExternalInteropProvider<TView> provider)
    {
        default(ArgumentNullException).ThrowIfNull(provider);

        this.provider = provider;
    }

    public override ExternalAdapterIdentity AdapterIdentity => this.provider.AdapterIdentity;

    public override ComputeExternalQueueScheduler Scheduler => this.provider.Scheduler;

    public override ExternalInteropCapabilities Capabilities => this.provider.Capabilities;

    public override void Initialize(in ExternalTimelineInitialization initialization)
    {
        this.provider.Initialize(in initialization);
    }

    public override void EnqueueSignal(ulong value)
    {
        this.provider.EnqueueSignal(value);
    }

    public override void FlushAfterSignal()
    {
        this.provider.FlushAfterSignal();
    }

    public override void EnqueueWait(ulong value)
    {
        this.provider.EnqueueWait(value);
    }

    public TView OpenSharedTexture(BorrowedSharedHandle resourceHandle, in ExternalTextureDescriptor descriptor)
    {
        return this.provider.OpenSharedTexture(resourceHandle, in descriptor);
    }

    protected override void OnDeviceTerminalCore(Exception reason)
    {
        this.provider.OnDeviceTerminal(reason);
    }

    protected override void DisposeProviderCore()
    {
        this.provider.Dispose();
    }
}
