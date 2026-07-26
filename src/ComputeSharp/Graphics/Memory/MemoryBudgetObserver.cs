using System;
using System.Threading;
using ComputeSharp.Win32;

namespace ComputeSharp.Memory;

internal sealed unsafe class MemoryBudgetObserver : IDisposable
{
    private readonly GraphicsDevice device;

    private readonly Thread thread;

    private readonly HANDLE eventHandle;

    private readonly uint cookie;

    private volatile bool isDisposed;

    private volatile Exception? failure;

    private MemoryBudgetObserver(GraphicsDevice device, HANDLE eventHandle, uint cookie)
    {
        this.device = device;
        this.eventHandle = eventHandle;
        this.cookie = cookie;

        this.thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ComputeSharp memory budget observer"
        };

        this.thread.Start();
    }

    public Exception? Failure => this.failure;

    public static MemoryBudgetObserver? TryCreate(GraphicsDevice device)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        HANDLE eventHandle = Windows.CreateEventW(null, Windows.FALSE, Windows.FALSE, null);

        if (eventHandle == HANDLE.NULL)
        {
            return null;
        }

        if (!device.TryRegisterMemoryBudgetNotification(eventHandle, out uint cookie))
        {
            _ = Windows.CloseHandle(eventHandle);

            return null;
        }

        return new MemoryBudgetObserver(device, eventHandle, cookie);
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

        this.device.UnregisterMemoryBudgetNotification(this.cookie);

        _ = Windows.SetEvent(this.eventHandle);

        this.thread.Join();

        _ = Windows.CloseHandle(this.eventHandle);
    }

    private void Run()
    {
        while (true)
        {
            _ = Windows.WaitForSingleObjectEx(this.eventHandle, Windows.INFINITE, Windows.FALSE);

            if (this.isDisposed)
            {
                return;
            }

            try
            {
                this.device.RefreshMemoryBudgetObservations();
            }
            catch (Exception e)
            {
                this.failure = e;

                return;
            }
        }
    }
}
