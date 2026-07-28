using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe partial class ComputeSubmissionExecutor
{
    public static ComputeSubmission Submit(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        int bundleIndex,
        ref SubmissionRetention retention)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(host);
        default(ArgumentNullException).ThrowIfNull(completionRegistry);

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        lock (device.HazardGate)
        {
            default(InvalidOperationException).ThrowIf(
                record.ReadState() is not SubmissionState.Recording,
                "The pending submission record is not being recorded.");

            Span<GraphicsResourceUsageEntry> usages = GetUsages(host, retention.ResourceUsages);

            RecordPrologue(host, usages, ref retention, out ulong copyFenceWaitValue);

            default(InvalidOperationException).ThrowIf(!record.TryCompleteValidation(), "The submission could not complete its validation.");

            Span<nint> d3D12CommandLists = stackalloc nint[CommandListLeaseSet.MaximumSegmentCount];

            int segmentCount = CollectCommandLists(ref retention, d3D12CommandLists);

            FencePoint completion = device.ExecutePipelineCommandLists(d3D12CommandLists[..segmentCount], copyFenceWaitValue);

            default(InvalidOperationException).ThrowIf(!record.TryMarkExecutionIssued(), "The submission could not be marked as issued.");
            default(InvalidOperationException).ThrowIf(!record.TryMarkCompletionSignaled(), "The submission completion could not be signaled.");

            ResourceHazardTracker.CommitResourceUsages(usages, completion);

            default(InvalidOperationException).ThrowIf(!record.TryCommitHazards(), "The submission hazards could not be committed.");

            ResourceGenerationPinTracker.ConvertToPendingSubmission(
                device,
                host.RecordingBundles.Storage,
                ref host.RecordingBundles.GetBundle(bundleIndex),
                usages);

            default(InvalidOperationException).ThrowIf(
                !completionRegistry.CommitAndPublish(host, recordIndex, completion, in retention, device.GetComputeFenceCompletedValue),
                "The submission could not be published.");

            return new ComputeSubmission(device, completion);
        }
    }

    private static void RecordPrologue(
        PipelineHostRuntime host,
        Span<GraphicsResourceUsageEntry> usages,
        ref SubmissionRetention retention,
        out ulong copyFenceWaitValue)
    {
        Span<ResourceBarrierPlanEntry> plan = stackalloc ResourceBarrierPlanEntry[usages.Length];

        int barrierCount = ResourceHazardTracker.PlanQueueDependencies(
            usages,
            ComputeQueueKind.Compute,
            plan,
            out copyFenceWaitValue);

        if (barrierCount == 0)
        {
            return;
        }

        default(InvalidOperationException).ThrowIf(
            retention.CommandLists.Count >= host.Descriptor.Structural.MaximumCommandListSegments,
            "The submission has no declared command list segment left for its prologue.");

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        bool isSegmentRetained = false;

        try
        {
            ResourceBarrierRecorder.RecordBarriers(d3D12CommandList, usages, plan[..barrierCount]);

            d3D12CommandList->Close().Assert();

            isSegmentRetained = retention.CommandLists.TryInsertFirst(
                (nint)d3D12CommandList,
                (nint)d3D12CommandAllocator,
                ComputeQueueKind.Compute);

            default(InvalidOperationException).ThrowIf(!isSegmentRetained, "The submission has no command list segment left for its prologue.");
        }
        finally
        {
            if (!isSegmentRetained)
            {
                host.CommandLists.Return(d3D12CommandList, isCommandListClosed: false);
            }
        }
    }

    public static bool TryReleaseCompleted(GraphicsDevice device, CompletionRegistry completionRegistry)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(completionRegistry);

        _ = completionRegistry.PromoteCompleted(device.GetComputeFenceCompletedValue());

        if (!completionRegistry.TryClaimCompletionReady(out PipelineHostRuntime host, out int recordIndex, out SubmissionRetention retention))
        {
            return false;
        }

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        bool hasRetiredGeneration = ReleaseRetention(host, retention);

        default(InvalidOperationException).ThrowIf(!record.TryCompleteReturn(), "The submission record could not complete its return.");

        host.ReturnPendingRecord(recordIndex);

        if (hasRetiredGeneration)
        {
            host.RunOwnedSlotMaintenance();
        }

        _ = host.TryCompleteDeferredRelease();

        return true;
    }

    public static Span<GraphicsResourceUsageEntry> GetUsages(PipelineHostRuntime host, UsageSetHandle usages)
    {
        if (usages.IsNone)
        {
            return default;
        }

        return ResourceUsageTracker.GetEntries(
            host.UsageSets.Storage,
            in host.UsageSets.GetSet(host.UsageSets.GetSetIndex(usages)));
    }

    private static bool ReleaseRetention(PipelineHostRuntime host, SubmissionRetention retention)
    {
        for (int i = retention.CommandLists.Count - 1; i >= 0; i--)
        {
            ref CommandListSegmentLease lease = ref CommandListLeaseSet.GetSegment(ref retention.CommandLists, i);

            if (lease.IsValid == 0)
            {
                continue;
            }

            host.CommandLists.Return((ID3D12GraphicsCommandList*)lease.CommandList, isCommandListClosed: true);
        }

        retention.ResourceLeases?.Release();

        bool hasRetiredGeneration = ReleasePendingSubmissionReferences(host, retention.ResourceUsages);

        if (!retention.ResourceUsages.IsNone)
        {
            ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(host.UsageSets.GetSetIndex(retention.ResourceUsages)));
        }

        return hasRetiredGeneration;
    }

    private static bool ReleasePendingSubmissionReferences(PipelineHostRuntime host, UsageSetHandle usages)
    {
        Span<GraphicsResourceUsageEntry> entries = GetUsages(host, usages);

        bool hasRetiredGeneration = false;

        for (int i = 0; i < entries.Length; i++)
        {
            ref readonly GraphicsResourceUsageEntry entry = ref entries[i];
            ref ResourceGenerationRecord record = ref entry.Set.Owner.GetResourceRecord(checked((int)entry.ResourceIndex));

            default(InvalidOperationException).ThrowIf(
                record.Id != entry.Generation,
                "The generation of a submitted resource no longer matches the tracked usage.");

            record.ReleasePendingSubmissionReference();

            if (!record.TryPromoteRetiredReady(host.Device.IsFenceCompleted(in record.RetirementFence)))
            {
                continue;
            }

            hasRetiredGeneration = true;

            if (entry.Set.Owner is ResourceGenerationOwner owner)
            {
                _ = owner.TryReleaseRetired(ResourceReleaseAuthority.NormalCompletion);
            }
        }

        return hasRetiredGeneration;
    }
}
