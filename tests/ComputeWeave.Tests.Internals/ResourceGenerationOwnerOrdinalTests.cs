using System;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class ResourceGenerationOwnerOrdinalTests
{
    private static ResourceGenerationOwner CreateOwner(GraphicsDevice device, int resourceCount)
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            device.TryReserveMemory(MemoryPlacement.Local, 256, out MemoryReservationToken token));

        return new ResourceGenerationOwner(
            device,
            device.ResourceIdentities,
            ComputeResourceRecovery.Discardable,
            in token,
            resourceCount,
            domain: null);
    }

    private static bool IsNativePointerNull(ResourceGenerationOwner owner, int resourceOrdinal)
    {
        return owner.GetResourceNativePointer(resourceOrdinal) is null;
    }

    private static void AssertRejectsOrdinal<TException>(ResourceGenerationOwner owner, int resourceOrdinal)
        where TException : Exception
    {
        _ = Assert.ThrowsException<TException>(() =>
        {
            _ = owner.GetResourceRecord(resourceOrdinal).Id;
        });

        _ = Assert.ThrowsException<TException>(() =>
        {
            _ = IsNativePointerNull(owner, resourceOrdinal);
        });

        Assert.IsNull(owner.TryGetExternalObject<IDisposable>(resourceOrdinal));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ASingleResourceGenerationRejectsEveryOrdinalOutsideIt(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), 1);

        AssertRejectsOrdinal<ArgumentOutOfRangeException>(owner, -1);
        AssertRejectsOrdinal<ArgumentOutOfRangeException>(owner, 1);
        AssertRejectsOrdinal<ArgumentOutOfRangeException>(owner, int.MaxValue);
        AssertRejectsOrdinal<ArgumentOutOfRangeException>(owner, int.MinValue);

        Assert.AreEqual(ResourceGenerationState.Constructing, owner.GetResourceRecord(0).ReadLifecycle());
        Assert.IsTrue(IsNativePointerNull(owner, 0));

        owner.ReleaseUnpublished();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AMultiResourceGenerationRejectsEveryOrdinalOutsideIt(Device device)
    {
        ResourceGenerationOwner owner = CreateOwner(device.Get(), 2);

        AssertRejectsOrdinal<IndexOutOfRangeException>(owner, -1);
        AssertRejectsOrdinal<IndexOutOfRangeException>(owner, 2);
        AssertRejectsOrdinal<IndexOutOfRangeException>(owner, int.MaxValue);
        AssertRejectsOrdinal<IndexOutOfRangeException>(owner, int.MinValue);

        Assert.AreNotEqual(owner.GetResourceRecord(0).Id, owner.GetResourceRecord(1).Id);
        Assert.IsTrue(IsNativePointerNull(owner, 0));
        Assert.IsTrue(IsNativePointerNull(owner, 1));

        owner.ReleaseUnpublished();
    }
}
