using System;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceGenerationExternalObjectTests
{
    private sealed class TrackedExternalObject : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private static ResourceGenerationOwner CreateOwner(GraphicsDevice device, ComputeInteropDomain? domain)
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            device.TryReserveMemory(MemoryPlacement.Local, 256, out MemoryReservationToken token));

        return new ResourceGenerationOwner(
            device,
            device.ResourceIdentities,
            ComputeResourceRecovery.Discardable,
            in token,
            1,
            domain);
    }

    private static ComputeInteropDomain RegisterDomain(GraphicsDevice device, FakeInteropScheduler scheduler)
    {
        return device.RegisterExternalDomain(new FakeInteropProvider(device, scheduler));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationWithoutExternalObjectsStartsReleased(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), domain: null);

        Assert.IsTrue(owner.GetResourceRecord(0).IsExternalObjectsReleased);

        _ = Assert.ThrowsException<InvalidOperationException>(() => owner.AttachExternalObject(0, new TrackedExternalObject()));

        owner.ReleaseUnpublished();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationWithExternalObjectsBlocksPromotionUntilTheyAreReleased(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        ResourceGenerationOwner owner = CreateOwner(graphicsDevice, domain);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        Assert.IsFalse(owner.GetResourceRecord(0).IsExternalObjectsReleased);
        Assert.AreEqual(0, externalObject.DisposeCount);

        Assert.IsTrue(owner.TryReleaseExternalObjects());

        Assert.AreEqual(1, externalObject.DisposeCount);
        Assert.IsTrue(owner.GetResourceRecord(0).IsExternalObjectsReleased);

        Assert.IsFalse(owner.TryReleaseExternalObjects());
        Assert.AreEqual(1, externalObject.DisposeCount);

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnUnpublishedGenerationStillReleasesItsExternalObject(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        ResourceGenerationOwner owner = CreateOwner(graphicsDevice, domain);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationOwnsOneExternalObjectPerResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        ResourceGenerationOwner owner = CreateOwner(graphicsDevice, domain);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        _ = Assert.ThrowsException<InvalidOperationException>(() => owner.AttachExternalObject(0, new TrackedExternalObject()));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => owner.AttachExternalObject(1, new TrackedExternalObject()));

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }
}
