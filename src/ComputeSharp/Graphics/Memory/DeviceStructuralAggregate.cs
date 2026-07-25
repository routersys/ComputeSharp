using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Memory;

internal readonly struct HostStructuralReservation(
    int recordingBundles,
    int pendingRecords,
    int usageSets,
    int commandListEntries,
    int usageEntryStorage,
    int preparedReplacementRecords,
    int deferredGenerationRecords,
    int planScalars)
{
    public int RecordingBundles { get; } = recordingBundles;

    public int PendingRecords { get; } = pendingRecords;

    public int UsageSets { get; } = usageSets;

    public int CommandListEntries { get; } = commandListEntries;

    public int UsageEntryStorage { get; } = usageEntryStorage;

    public int PreparedReplacementRecords { get; } = preparedReplacementRecords;

    public int DeferredGenerationRecords { get; } = deferredGenerationRecords;

    public int PlanScalars { get; } = planScalars;
}

internal readonly struct ResourceSetStructuralReservation(
    int sharedTextureSlots,
    int maintenanceRecords,
    int persistentLeaseCapacity,
    int planScalars)
{
    public int SharedTextureSlots { get; } = sharedTextureSlots;

    public int MaintenanceRecords { get; } = maintenanceRecords;

    public int PersistentLeaseCapacity { get; } = persistentLeaseCapacity;

    public int PlanScalars { get; } = planScalars;
}

internal struct DeviceStructuralAggregate
{
    private const int PersistentLeasesPerSharedTextureSlot = 2;

    public int RecordingBundleCount;

    public int PendingRecordCount;

    public int UsageSetCount;

    public int CommandListEntryCount;

    public int UsageEntryStorageCount;

    public int PreparedReplacementRecordCount;

    public int DeferredGenerationRecordCount;

    public int HostPlanScalarCount;

    public int SharedTextureSlotCount;

    public int MaintenanceRecordCount;

    public int PersistentLeaseCapacity;

    public int ResourceSetPlanScalarCount;

    public bool TryReserveHost(
        in PipelineHostDescriptor host,
        int maximumPendingSubmissions,
        int planScalarCount,
        out HostStructuralReservation reservation)
    {
        int maximumConcurrentInvocations = host.MaximumConcurrentInvocations;
        int maximumTrackedResourceCount = host.Structural.MaximumTrackedResourceCount;
        int maximumCommandListSegments = host.Structural.MaximumCommandListSegments;
        int ownedSlotCount = host.Structural.OwnedSlotCount;

        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(maximumConcurrentInvocations);
        default(ArgumentOutOfRangeException).ThrowIfLessThan(maximumPendingSubmissions, maximumConcurrentInvocations);
        default(ArgumentOutOfRangeException).ThrowIfNegative(maximumTrackedResourceCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(maximumCommandListSegments);
        default(ArgumentOutOfRangeException).ThrowIfNegative(ownedSlotCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(planScalarCount);

        long commandListEntries = (long)maximumPendingSubmissions * maximumCommandListSegments;
        long usageEntryStorage = (long)maximumPendingSubmissions * maximumTrackedResourceCount;

        if (!TryAdd(this.RecordingBundleCount, maximumConcurrentInvocations, out int recordingBundleCount) ||
            !TryAdd(this.PendingRecordCount, maximumPendingSubmissions, out int pendingRecordCount) ||
            !TryAdd(this.UsageSetCount, maximumPendingSubmissions, out int usageSetCount) ||
            !TryAdd(this.CommandListEntryCount, commandListEntries, out int commandListEntryCount) ||
            !TryAdd(this.UsageEntryStorageCount, usageEntryStorage, out int usageEntryStorageCount) ||
            !TryAdd(this.PreparedReplacementRecordCount, ownedSlotCount, out int preparedReplacementRecordCount) ||
            !TryAdd(this.DeferredGenerationRecordCount, ownedSlotCount, out int deferredGenerationRecordCount) ||
            !TryAdd(this.HostPlanScalarCount, planScalarCount, out int hostPlanScalarCount))
        {
            reservation = default;

            return false;
        }

        this.RecordingBundleCount = recordingBundleCount;
        this.PendingRecordCount = pendingRecordCount;
        this.UsageSetCount = usageSetCount;
        this.CommandListEntryCount = commandListEntryCount;
        this.UsageEntryStorageCount = usageEntryStorageCount;
        this.PreparedReplacementRecordCount = preparedReplacementRecordCount;
        this.DeferredGenerationRecordCount = deferredGenerationRecordCount;
        this.HostPlanScalarCount = hostPlanScalarCount;

        reservation = new HostStructuralReservation(
            maximumConcurrentInvocations,
            maximumPendingSubmissions,
            maximumPendingSubmissions,
            (int)commandListEntries,
            (int)usageEntryStorage,
            ownedSlotCount,
            ownedSlotCount,
            planScalarCount);

        return true;
    }

    public void ReleaseHost(in HostStructuralReservation reservation)
    {
        this.RecordingBundleCount = Subtract(this.RecordingBundleCount, reservation.RecordingBundles);
        this.PendingRecordCount = Subtract(this.PendingRecordCount, reservation.PendingRecords);
        this.UsageSetCount = Subtract(this.UsageSetCount, reservation.UsageSets);
        this.CommandListEntryCount = Subtract(this.CommandListEntryCount, reservation.CommandListEntries);
        this.UsageEntryStorageCount = Subtract(this.UsageEntryStorageCount, reservation.UsageEntryStorage);
        this.PreparedReplacementRecordCount = Subtract(this.PreparedReplacementRecordCount, reservation.PreparedReplacementRecords);
        this.DeferredGenerationRecordCount = Subtract(this.DeferredGenerationRecordCount, reservation.DeferredGenerationRecords);
        this.HostPlanScalarCount = Subtract(this.HostPlanScalarCount, reservation.PlanScalars);
    }

    public bool TryReserveResourceSet(
        in InteropResourceSetDescriptor resourceSet,
        int planScalarCount,
        out ResourceSetStructuralReservation reservation)
    {
        int sharedTextureSlotCount = resourceSet.Structural.SharedTextureSlotCount;

        default(ArgumentOutOfRangeException).ThrowIfNegative(sharedTextureSlotCount);
        default(ArgumentOutOfRangeException).ThrowIfNegative(planScalarCount);

        long persistentLeaseCapacity = (long)sharedTextureSlotCount * PersistentLeasesPerSharedTextureSlot;

        if (!TryAdd(this.SharedTextureSlotCount, sharedTextureSlotCount, out int sharedTextureSlots) ||
            !TryAdd(this.MaintenanceRecordCount, sharedTextureSlotCount, out int maintenanceRecords) ||
            !TryAdd(this.PersistentLeaseCapacity, persistentLeaseCapacity, out int persistentLeases) ||
            !TryAdd(this.ResourceSetPlanScalarCount, planScalarCount, out int planScalars))
        {
            reservation = default;

            return false;
        }

        this.SharedTextureSlotCount = sharedTextureSlots;
        this.MaintenanceRecordCount = maintenanceRecords;
        this.PersistentLeaseCapacity = persistentLeases;
        this.ResourceSetPlanScalarCount = planScalars;

        reservation = new ResourceSetStructuralReservation(
            sharedTextureSlotCount,
            sharedTextureSlotCount,
            (int)persistentLeaseCapacity,
            planScalarCount);

        return true;
    }

    public void ReleaseResourceSet(in ResourceSetStructuralReservation reservation)
    {
        this.SharedTextureSlotCount = Subtract(this.SharedTextureSlotCount, reservation.SharedTextureSlots);
        this.MaintenanceRecordCount = Subtract(this.MaintenanceRecordCount, reservation.MaintenanceRecords);
        this.PersistentLeaseCapacity = Subtract(this.PersistentLeaseCapacity, reservation.PersistentLeaseCapacity);
        this.ResourceSetPlanScalarCount = Subtract(this.ResourceSetPlanScalarCount, reservation.PlanScalars);
    }

    private static bool TryAdd(int current, long delta, out int result)
    {
        long total = current + delta;

        if (total > int.MaxValue)
        {
            result = 0;

            return false;
        }

        result = (int)total;

        return true;
    }

    private static int Subtract(int current, int delta)
    {
        default(InvalidOperationException).ThrowIf(delta > current, "The device structural aggregate is below the released reservation.");

        return current - delta;
    }
}
