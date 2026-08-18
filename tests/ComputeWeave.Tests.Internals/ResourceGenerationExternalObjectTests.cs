using System;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Resources.Plans;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Win32;
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
    public unsafe void AnUnpublishedGenerationReleasesItsExternalObjectAfterConstruction(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.DescribeInteropSharedTexture(graphicsDevice, 16, 16, out ComputeGenerationDeclaration declaration));
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            graphicsDevice.TryReserveMemory(declaration.Placement, declaration.SizeInBytes, out MemoryReservationToken token));

        ResourceGenerationOwner owner = new(
            graphicsDevice,
            graphicsDevice.ResourceIdentities,
            ComputeResourceRecovery.Discardable,
            in token,
            1,
            domain);

        Assert.IsTrue(graphicsDevice.TryCreateCommittedResource(in declaration.Description, out ComPtr<ID3D12Resource> created) >= 0);

        using ComPtr<ID3D12Resource> d3D12Resource = created;

        ReadWriteTexture2D<Bgra32, Float4> texture = new(
            graphicsDevice,
            d3D12Resource.Get(),
            16,
            16,
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS);

        owner.AttachResource(texture, d3D12Resource.Get(), TrackedResourceState.Common, declaration.SizeInBytes);

        TrackedExternalObject externalObject = new();

        owner.AttachExternalObject(0, externalObject);
        owner.CompleteConstruction();

        owner.ReleaseUnpublished();

        Assert.AreEqual(1, externalObject.DisposeCount);
        Assert.AreEqual(ResourceGenerationState.Released, owner.GetResourceRecord(0).ReadLifecycle());
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
