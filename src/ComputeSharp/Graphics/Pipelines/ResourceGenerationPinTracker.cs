using System;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Interop;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe class ResourceGenerationPinTracker
{
    public static bool TryPin(
        GraphicsDevice device,
        Span<ResourceGenerationPin> storage,
        ref RecordingBundleEntry bundle,
        IGraphicsResource resource)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(resource);

        if (resource is not ID3D12ReadOnlyResource readOnlyResource ||
            resource is not IGenerationBoundResource boundResource)
        {
            return false;
        }

        _ = readOnlyResource.ValidateAndGetID3D12Resource(device, out ReferenceTracker.Lease lease);

        ResourceGenerationPin pin;

        try
        {
            if (!boundResource.TryGetGenerationBinding(out ResourceUsageBinding binding))
            {
                return false;
            }

            int resourceIndex = checked((int)binding.ResourceIndex);

            ref ResourceGenerationRecord record = ref binding.Set.Owner.GetResourceRecord(resourceIndex);

            if (record.Id != binding.Generation || !record.TryAcquireRecordingReference())
            {
                return false;
            }

            pin = new ResourceGenerationPin(binding.Set, binding.Generation, resourceIndex);
        }
        finally
        {
            lease.Dispose();
        }

        return TryAdd(device, storage, ref bundle, in pin);
    }

    public static bool TryAdd(
        GraphicsDevice device,
        Span<ResourceGenerationPin> storage,
        ref RecordingBundleEntry bundle,
        in ResourceGenerationPin pin)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        if (bundle.Count >= bundle.Capacity)
        {
            Release(device, in pin);

            return false;
        }

        storage[checked(bundle.StorageOffset + bundle.Count)] = pin;

        bundle.Count++;

        return true;
    }

    public static void Rollback(GraphicsDevice device, Span<ResourceGenerationPin> storage, ref RecordingBundleEntry bundle)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        Span<ResourceGenerationPin> pins = GetPins(storage, in bundle);

        for (int i = pins.Length - 1; i >= 0; i--)
        {
            Release(device, in pins[i]);
        }

        pins.Clear();

        bundle.Count = 0;
    }

    public static void ConvertToPendingSubmission(
        GraphicsDevice device,
        Span<ResourceGenerationPin> storage,
        ref RecordingBundleEntry bundle,
        ReadOnlySpan<GraphicsResourceUsageEntry> usages)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        Span<ResourceGenerationPin> pins = GetPins(storage, in bundle);

        for (int i = 0; i < pins.Length; i++)
        {
            ref readonly ResourceGenerationPin pin = ref pins[i];
            ref ResourceGenerationRecord record = ref GetPinnedRecord(in pin);

            if (IsObserved(usages, pin.GenerationId))
            {
                record.ConvertRecordingToPendingSubmission();
            }
            else
            {
                record.ReleaseRecordingReference();
            }

            _ = record.TryPromoteRetiredReady(device.IsFenceCompleted(in record.RetirementFence));
        }

        pins.Clear();

        bundle.Count = 0;
    }

    public static Span<ResourceGenerationPin> GetPins(Span<ResourceGenerationPin> storage, in RecordingBundleEntry bundle)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(bundle.StorageOffset);
        default(ArgumentOutOfRangeException).ThrowIfNegative(bundle.Count);
        default(ArgumentOutOfRangeException).ThrowIfGreaterThan(bundle.Count, bundle.Capacity);

        return storage.Slice(bundle.StorageOffset, bundle.Count);
    }

    private static bool IsObserved(ReadOnlySpan<GraphicsResourceUsageEntry> usages, ResourceGenerationId generation)
    {
        for (int i = 0; i < usages.Length; i++)
        {
            if (usages[i].Generation == generation)
            {
                return true;
            }
        }

        return false;
    }

    private static void Release(GraphicsDevice device, in ResourceGenerationPin pin)
    {
        ref ResourceGenerationRecord record = ref GetPinnedRecord(in pin);

        record.ReleaseRecordingReference();

        _ = record.TryPromoteRetiredReady(device.IsFenceCompleted(in record.RetirementFence));
    }

    private static ref ResourceGenerationRecord GetPinnedRecord(in ResourceGenerationPin pin)
    {
        ref ResourceGenerationRecord record = ref pin.Handle.Owner.GetResourceRecord(pin.ResourceIndex);

        default(InvalidOperationException).ThrowIf(record.Id != pin.GenerationId, "The pinned generation no longer matches.");

        return ref record;
    }
}
