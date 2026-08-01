using System;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe partial class Direct3D11ProviderHarnessTests
{
    private static Direct3D11ImmediateContext CreateContext(GraphicsDevice device)
    {
        return Direct3D11ImmediateContext.Create(device.Luid.ToInt64());
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OpensTheSharedTimelineFromDirect3D11(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);

        Direct3D11InteropProvider provider = new(context);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.IsTrue(provider.OpenedSharedFence);
        Assert.AreNotEqual(0ul, domain.Id.Value);
        Assert.AreEqual(
            ExternalInteropCapabilities.SharedFence |
            ExternalInteropCapabilities.SharedTexture2D |
            ExternalInteropCapabilities.SingleImmediateContextOrdering |
            ExternalInteropCapabilities.PersistentExternalViewOrdering,
            domain.Capabilities);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ProvidersSharingAnImmediateContextShareOneScheduler(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);

        Direct3D11InteropProvider first = new(context);
        Direct3D11InteropProvider second = new(context);

        Assert.AreSame(first.Scheduler, second.Scheduler);

        using ComputeInteropDomain firstDomain = graphicsDevice.RegisterExternalDomain(first);
        using ComputeInteropDomain secondDomain = graphicsDevice.RegisterExternalDomain(second);

        Assert.AreNotEqual(firstDomain.Id.Value, secondDomain.Id.Value);
        Assert.IsTrue(first.OpenedSharedFence);
        Assert.IsTrue(second.OpenedSharedFence);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ProvidersOnDistinctImmediateContextsDoNotShareAScheduler(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext first = CreateContext(graphicsDevice);
        using Direct3D11ImmediateContext second = CreateContext(graphicsDevice);

        Assert.AreNotSame(first.Scheduler, second.Scheduler);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TheSharedSchedulerOutlivesItsOwnerUntilEveryDomainIsDisposed(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        Direct3D11ExternalQueueScheduler scheduler = context.Scheduler;

        ComputeInteropDomain firstDomain = graphicsDevice.RegisterExternalDomain(new Direct3D11InteropProvider(context));
        ComputeInteropDomain secondDomain = graphicsDevice.RegisterExternalDomain(new Direct3D11InteropProvider(context));

        context.Dispose();

        Assert.AreEqual(0, scheduler.DisposeCount);

        firstDomain.Dispose();

        Assert.AreEqual(0, scheduler.DisposeCount);

        secondDomain.Dispose();

        Assert.AreEqual(1, scheduler.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TheImmediateContextReservationIsExclusive(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(new Direct3D11InteropProvider(context));

        Assert.IsTrue(SchedulerRegistration.TryAcquire(context.Scheduler, out SchedulerRegistration? registration));

        try
        {
            registration.EnterReservation();

            try
            {
                _ = Assert.ThrowsException<InvalidOperationException>(registration.EnterReservation);
            }
            finally
            {
                registration.ExitReservation();
            }

            registration.EnterReservation();
            registration.ExitReservation();
        }
        finally
        {
            registration.Release();
        }
    }
}
