using System;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

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

    private static ResourceGenerationOwner CreateOwner(GraphicsDevice device, bool hasExternalObjects)
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
            hasExternalObjects);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationWithoutExternalObjectsStartsReleased(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), hasExternalObjects: false);

        Assert.AreEqual(1, owner.GetResourceRecord(0).ExternalObjectsReleased);

        _ = Assert.ThrowsException<InvalidOperationException>(() => owner.AttachExternalObject(0, new TrackedExternalObject()));

        owner.ReleaseUnpublished();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationWithExternalObjectsBlocksPromotionUntilTheyAreReleased(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), hasExternalObjects: true);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        Assert.AreEqual(0, owner.GetResourceRecord(0).ExternalObjectsReleased);
        Assert.AreEqual(0, externalObject.DisposeCount);

        Assert.IsTrue(owner.TryReleaseExternalObjects());

        Assert.AreEqual(1, externalObject.DisposeCount);
        Assert.AreEqual(1, owner.GetResourceRecord(0).ExternalObjectsReleased);

        Assert.IsFalse(owner.TryReleaseExternalObjects());
        Assert.AreEqual(1, externalObject.DisposeCount);

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AnUnpublishedGenerationStillReleasesItsExternalObject(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), hasExternalObjects: true);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AGenerationOwnsOneExternalObjectPerResource(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), hasExternalObjects: true);
        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);

        _ = Assert.ThrowsException<InvalidOperationException>(() => owner.AttachExternalObject(0, new TrackedExternalObject()));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => owner.AttachExternalObject(1, new TrackedExternalObject()));

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
    }
}
