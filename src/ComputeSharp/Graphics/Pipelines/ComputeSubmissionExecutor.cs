using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe class ComputeSubmissionExecutor
{
    public static void Prepare(
        PipelineHostRuntime host,
        int recordIndex,
        ref SubmissionRetention retention,
        out ulong copyFenceWaitValue)
    {
        default(ArgumentNullException).ThrowIfNull(host);

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        default(InvalidOperationException).ThrowIf(
            record.ReadState() is not SubmissionState.Recording,
            "The pending submission record is not being recorded.");

        Span<GraphicsResourceUsageEntry> usages = GetUsages(host, retention.ResourceUsages);
        Span<ResourceBarrierPlanEntry> plan = stackalloc ResourceBarrierPlanEntry[usages.Length];

        int barrierCount = ResourceHazardTracker.PrepareResourceUsages(
            usages,
            ComputeQueueKind.Compute,
            plan,
            out copyFenceWaitValue);

        if (barrierCount > 0)
        {
            host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

            bool isSegmentRetained = false;

            try
            {
                ResourceBarrierRecorder.RecordPrologue(d3D12CommandList, usages, plan[..barrierCount]);

                d3D12CommandList->Close().Assert();

                isSegmentRetained = retention.CommandLists.TryInsertFirst(
                    (nint)d3D12CommandList,
                    (nint)d3D12CommandAllocator,
                    ComputeQueueKind.Compute);

                default(InvalidOperationException).ThrowIf(
                    !isSegmentRetained,
                    "The submission has no command list segment left for its prologue.");
            }
            finally
            {
                if (!isSegmentRetained)
                {
                    host.CommandLists.Return(d3D12CommandList, isCommandListClosed: false);
                }
            }
        }

        default(InvalidOperationException).ThrowIf(!record.TryCompleteValidation(), "The submission could not complete its validation.");
    }

    public static ComputeSubmission Submit(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        ulong copyFenceWaitValue,
        in SubmissionRetention retention)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(host);
        default(ArgumentNullException).ThrowIfNull(completionRegistry);

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        default(InvalidOperationException).ThrowIf(
            record.ReadState() is not SubmissionState.Prepared,
            "The pending submission record is not prepared for execution.");

        Span<nint> d3D12CommandLists = stackalloc nint[CommandListLeaseSet.MaximumSegmentCount];
        SubmissionRetention segments = retention;
        int segmentCount = 0;

        for (int i = 0; i < segments.CommandLists.Count; i++)
        {
            ref CommandListSegmentLease lease = ref CommandListLeaseSet.GetSegment(ref segments.CommandLists, i);

            if (lease.IsValid != 0)
            {
                d3D12CommandLists[segmentCount++] = lease.CommandList;
            }
        }

        FencePoint completion = device.ExecutePipelineCommandLists(d3D12CommandLists[..segmentCount], copyFenceWaitValue);

        default(InvalidOperationException).ThrowIf(!record.TryMarkExecutionIssued(), "The submission could not be marked as issued.");
        default(InvalidOperationException).ThrowIf(!record.TryMarkCompletionSignaled(), "The submission completion could not be signaled.");

        if (!retention.ResourceUsages.IsNone)
        {
            ResourceHazardTracker.CommitResourceUsages(GetUsages(host, retention.ResourceUsages), completion);
        }

        default(InvalidOperationException).ThrowIf(!record.TryCommitHazards(), "The submission hazards could not be committed.");
        default(InvalidOperationException).ThrowIf(
            !completionRegistry.CommitAndPublish(host, recordIndex, completion, in retention, device.GetComputeFenceCompletedValue),
            "The submission could not be published.");

        return new ComputeSubmission(device, completion);
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

        ReleaseRetention(host, retention);

        default(InvalidOperationException).ThrowIf(!record.TryCompleteReturn(), "The submission record could not complete its return.");

        host.ReturnPendingRecord(recordIndex);

        return true;
    }

    private static Span<GraphicsResourceUsageEntry> GetUsages(PipelineHostRuntime host, UsageSetHandle usages)
    {
        return ResourceUsageTracker.GetEntries(
            host.UsageSets.Storage,
            in host.UsageSets.GetSet(host.UsageSets.GetSetIndex(usages)));
    }

    private static void ReleaseRetention(PipelineHostRuntime host, SubmissionRetention retention)
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

        if (!retention.ResourceUsages.IsNone)
        {
            ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(host.UsageSets.GetSetIndex(retention.ResourceUsages)));
        }
    }
}
