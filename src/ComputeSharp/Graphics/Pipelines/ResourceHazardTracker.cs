using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal enum ResourceBarrierKind : byte
{
    None = 0,
    Transition = 1,
    UnorderedAccess = 2
}

internal readonly struct ResourceBarrierPlanEntry(
    int usageIndex,
    ResourceBarrierKind kind,
    TrackedResourceState beforeState,
    TrackedResourceState afterState)
{
    public int UsageIndex { get; } = usageIndex;

    public ResourceBarrierKind Kind { get; } = kind;

    public TrackedResourceState BeforeState { get; } = beforeState;

    public TrackedResourceState AfterState { get; } = afterState;
}

internal static class ResourceHazardTracker
{
    public static int PrepareResourceUsages(
        Span<GraphicsResourceUsageEntry> usages,
        ComputeQueueKind targetQueue,
        Span<ResourceBarrierPlanEntry> prologue,
        out ulong crossQueueWaitValue)
    {
        default(ArgumentException).ThrowIf(prologue.Length < usages.Length, nameof(prologue));

        int barrierCount = 0;

        crossQueueWaitValue = 0;

        for (int i = 0; i < usages.Length; i++)
        {
            ref GraphicsResourceUsageEntry usage = ref usages[i];
            ref ResourceGenerationRecord record = ref usage.Set.Owner.GetResourceRecord(checked((int)usage.ResourceIndex));

            default(InvalidOperationException).ThrowIf(
                usage.FirstState is TrackedResourceState.Unknown || usage.FinalState is TrackedResourceState.Unknown,
                "The tracked resource usage has no recorded state.");

            AccumulateCrossQueueWait(in record, usage.Access, targetQueue, ref crossQueueWaitValue);

            if (record.D3D12State != usage.FirstState)
            {
                prologue[barrierCount++] = new ResourceBarrierPlanEntry(
                    i,
                    ResourceBarrierKind.Transition,
                    record.D3D12State,
                    usage.FirstState);
            }
            else if (record.D3D12State is TrackedResourceState.UnorderedAccess &&
                     usage.FirstState is TrackedResourceState.UnorderedAccess &&
                     !record.LastWrite.IsNone)
            {
                prologue[barrierCount++] = new ResourceBarrierPlanEntry(
                    i,
                    ResourceBarrierKind.UnorderedAccess,
                    record.D3D12State,
                    usage.FirstState);
            }
        }

        return barrierCount;
    }

    public static void CommitResourceUsages(Span<GraphicsResourceUsageEntry> usages, FencePoint completion)
    {
        default(ArgumentException).ThrowIf(completion.IsNone, nameof(completion));

        for (int i = 0; i < usages.Length; i++)
        {
            ref GraphicsResourceUsageEntry usage = ref usages[i];
            ref ResourceGenerationRecord record = ref usage.Set.Owner.GetResourceRecord(checked((int)usage.ResourceIndex));

            if (usage.Access is ComputeResourceAccess.Read)
            {
                if (completion.Queue is ComputeQueueKind.Compute)
                {
                    record.LastComputeRead = completion;
                }
                else
                {
                    record.LastCopyRead = completion;
                }
            }
            else
            {
                record.LastWrite = completion;
                record.LastComputeRead = FencePoint.None;
                record.LastCopyRead = FencePoint.None;
            }

            record.D3D12State = usage.FinalState;
        }
    }

    private static void AccumulateCrossQueueWait(
        in ResourceGenerationRecord record,
        ComputeResourceAccess access,
        ComputeQueueKind targetQueue,
        ref ulong crossQueueWaitValue)
    {
        if (!record.LastWrite.IsNone && record.LastWrite.Queue != targetQueue)
        {
            crossQueueWaitValue = Math.Max(crossQueueWaitValue, record.LastWrite.Value);
        }

        if (access is ComputeResourceAccess.Read)
        {
            return;
        }

        if (!record.LastComputeRead.IsNone && targetQueue is not ComputeQueueKind.Compute)
        {
            crossQueueWaitValue = Math.Max(crossQueueWaitValue, record.LastComputeRead.Value);
        }

        if (!record.LastCopyRead.IsNone && targetQueue is not ComputeQueueKind.Copy)
        {
            crossQueueWaitValue = Math.Max(crossQueueWaitValue, record.LastCopyRead.Value);
        }
    }
}
