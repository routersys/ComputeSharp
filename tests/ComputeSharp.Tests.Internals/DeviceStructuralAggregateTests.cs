using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class DeviceStructuralAggregateTests
{
    private static ResourcePlanFieldDescriptor Field(uint fieldOrdinal, uint slotResourceIndex, ResourcePlanDimensionKind dimensionKind)
    {
        return new ResourcePlanFieldDescriptor(
            fieldOrdinal,
            slotResourceIndex,
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            "memberLength",
            dimensionKind);
    }

    private static OwnedSlotDescriptor BufferSlot(uint ordinal)
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(ordinal),
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Buffer,
            ComputeResourceRecovery.Discardable,
            new[] { Field(0, 0, ResourcePlanDimensionKind.Length) });
    }

    private static OwnedSlotDescriptor TextureSlot(uint ordinal)
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(ordinal),
            "Member",
            "ComputeSharp.ReadWriteTexture2D`1",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Texture2D,
            ComputeResourceRecovery.Discardable,
            new[]
            {
                Field(0, 0, ResourcePlanDimensionKind.Width),
                Field(1, 0, ResourcePlanDimensionKind.Height)
            });
    }

    private static PipelineHostDescriptor Host(
        int maximumConcurrentInvocations,
        int maximumTrackedResourceCount,
        int maximumCommandListSegments,
        params OwnedSlotDescriptor[] slots)
    {
        return new PipelineHostDescriptor(
            new PipelineSchemaVersion(1, 0, 1),
            default,
            "Ukiyoe.Host",
            maximumConcurrentInvocations,
            new StaticStructuralRequirements(maximumTrackedResourceCount, maximumCommandListSegments, slots.Length),
            default,
            slots);
    }

    private static SharedTextureContractDescriptor SharedTexture(uint ordinal)
    {
        return new SharedTextureContractDescriptor(
            new SlotOrdinal(ordinal),
            "Source",
            "ComputeSharp.SharedTextureSlot`3",
            ComputeResourceResizePolicy.Exact,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost);
    }

    private static InteropResourceSetDescriptor ResourceSet(params SharedTextureContractDescriptor[] sharedTextures)
    {
        return new InteropResourceSetDescriptor(
            new PipelineSchemaVersion(1, 0, 1),
            default,
            "Ukiyoe.ResourceSet",
            new ResourceSetStructuralRequirements(sharedTextures.Length),
            sharedTextures);
    }

    [TestMethod]
    public void ReservesHostBaselineFromDescriptor()
    {
        PipelineHostDescriptor host = Host(2, 5, 3, BufferSlot(0), TextureSlot(1));
        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveHost(host, maximumPendingSubmissions: 4, planScalarCount: 9, out HostStructuralReservation reservation));

        Assert.AreEqual(2, aggregate.RecordingBundleCount);
        Assert.AreEqual(4, aggregate.PendingRecordCount);
        Assert.AreEqual(4, aggregate.UsageSetCount);
        Assert.AreEqual(12, aggregate.CommandListEntryCount);
        Assert.AreEqual(20, aggregate.UsageEntryStorageCount);
        Assert.AreEqual(2, aggregate.PreparedReplacementRecordCount);
        Assert.AreEqual(2, aggregate.DeferredGenerationRecordCount);
        Assert.AreEqual(9, aggregate.HostPlanScalarCount);

        Assert.AreEqual(2, reservation.RecordingBundles);
        Assert.AreEqual(4, reservation.PendingRecords);
        Assert.AreEqual(12, reservation.CommandListEntries);
        Assert.AreEqual(20, reservation.UsageEntryStorage);
    }

    [TestMethod]
    public void AccumulatesAndReleasesEveryHostReservation()
    {
        PipelineHostDescriptor first = Host(1, 2, 1, BufferSlot(0));
        PipelineHostDescriptor second = Host(3, 4, 2, BufferSlot(0), TextureSlot(1));
        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveHost(first, 2, 3, out HostStructuralReservation firstReservation));
        Assert.IsTrue(aggregate.TryReserveHost(second, 5, 9, out HostStructuralReservation secondReservation));

        Assert.AreEqual(4, aggregate.RecordingBundleCount);
        Assert.AreEqual(7, aggregate.PendingRecordCount);
        Assert.AreEqual(12, aggregate.CommandListEntryCount);
        Assert.AreEqual(24, aggregate.UsageEntryStorageCount);
        Assert.AreEqual(3, aggregate.PreparedReplacementRecordCount);
        Assert.AreEqual(12, aggregate.HostPlanScalarCount);

        aggregate.ReleaseHost(secondReservation);
        aggregate.ReleaseHost(firstReservation);

        Assert.AreEqual(0, aggregate.RecordingBundleCount);
        Assert.AreEqual(0, aggregate.PendingRecordCount);
        Assert.AreEqual(0, aggregate.UsageSetCount);
        Assert.AreEqual(0, aggregate.CommandListEntryCount);
        Assert.AreEqual(0, aggregate.UsageEntryStorageCount);
        Assert.AreEqual(0, aggregate.PreparedReplacementRecordCount);
        Assert.AreEqual(0, aggregate.DeferredGenerationRecordCount);
        Assert.AreEqual(0, aggregate.HostPlanScalarCount);
    }

    [TestMethod]
    public void RejectsHostReservationOnOverflowWithoutMutating()
    {
        PipelineHostDescriptor host = Host(1, int.MaxValue, 1, BufferSlot(0));
        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveHost(host, maximumPendingSubmissions: 1, planScalarCount: 3, out _));
        Assert.IsFalse(aggregate.TryReserveHost(host, maximumPendingSubmissions: 2, planScalarCount: 3, out HostStructuralReservation reservation));

        Assert.AreEqual(0, reservation.PendingRecords);
        Assert.AreEqual(1, aggregate.RecordingBundleCount);
        Assert.AreEqual(1, aggregate.PendingRecordCount);
        Assert.AreEqual(int.MaxValue, aggregate.UsageEntryStorageCount);
        Assert.AreEqual(3, aggregate.HostPlanScalarCount);
    }

    [TestMethod]
    public void RejectsInvalidHostContract()
    {
        DeviceStructuralAggregate aggregate = default;

        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => aggregate.TryReserveHost(Host(0, 1, 1, BufferSlot(0)), 1, 3, out _));

        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => aggregate.TryReserveHost(Host(2, 1, 1, BufferSlot(0)), 1, 3, out _));

        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => aggregate.TryReserveHost(Host(1, 1, 1, BufferSlot(0)), 1, -1, out _));
    }

    [TestMethod]
    public void ReservesResourceSetBaselineFromDescriptor()
    {
        InteropResourceSetDescriptor resourceSet = ResourceSet(SharedTexture(0), SharedTexture(1));
        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveResourceSet(resourceSet, planScalarCount: 12, out ResourceSetStructuralReservation reservation));

        Assert.AreEqual(2, aggregate.SharedTextureSlotCount);
        Assert.AreEqual(2, aggregate.MaintenanceRecordCount);
        Assert.AreEqual(4, aggregate.PersistentLeaseCapacity);
        Assert.AreEqual(12, aggregate.ResourceSetPlanScalarCount);

        aggregate.ReleaseResourceSet(reservation);

        Assert.AreEqual(0, aggregate.SharedTextureSlotCount);
        Assert.AreEqual(0, aggregate.MaintenanceRecordCount);
        Assert.AreEqual(0, aggregate.PersistentLeaseCapacity);
        Assert.AreEqual(0, aggregate.ResourceSetPlanScalarCount);
    }

    [TestMethod]
    public void RejectsReleaseBelowReservedAggregate()
    {
        InteropResourceSetDescriptor resourceSet = ResourceSet(SharedTexture(0));
        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveResourceSet(resourceSet, planScalarCount: 6, out ResourceSetStructuralReservation reservation));

        aggregate.ReleaseResourceSet(reservation);

        _ = Assert.ThrowsException<InvalidOperationException>(() => aggregate.ReleaseResourceSet(reservation));
    }

    [TestMethod]
    public void MatchesPlanScalarCapacityDerivedFromDescriptors()
    {
        PipelineHostDescriptor host = Host(1, 1, 1, BufferSlot(0), TextureSlot(1));
        SlotResourcePlanStateRecord[] hostStates = new SlotResourcePlanStateRecord[host.Slots.Length];

        int hostPlanScalarCount = SlotResourcePlanStorage.CreateHostPlanStates(host, hostStates);

        InteropResourceSetDescriptor resourceSet = ResourceSet(SharedTexture(0), SharedTexture(1));
        SlotResourcePlanStateRecord[] resourceSetStates = new SlotResourcePlanStateRecord[resourceSet.SharedTextures.Length];

        int resourceSetPlanScalarCount = SlotResourcePlanStorage.CreateResourceSetPlanStates(resourceSet, resourceSetStates);

        DeviceStructuralAggregate aggregate = default;

        Assert.IsTrue(aggregate.TryReserveHost(host, 1, hostPlanScalarCount, out _));
        Assert.IsTrue(aggregate.TryReserveResourceSet(resourceSet, resourceSetPlanScalarCount, out _));

        Assert.AreEqual(9, aggregate.HostPlanScalarCount);
        Assert.AreEqual(12, aggregate.ResourceSetPlanScalarCount);
    }
}
