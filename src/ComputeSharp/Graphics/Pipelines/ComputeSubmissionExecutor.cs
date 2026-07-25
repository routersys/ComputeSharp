using System;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe class ComputeSubmissionExecutor
{
    public static FencePoint Submit(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        ID3D12GraphicsCommandList* d3D12CommandList,
        ulong copyFenceWaitValue,
        in SubmissionRetention retention)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(host);
        default(ArgumentNullException).ThrowIfNull(completionRegistry);
        default(ArgumentNullException).ThrowIf(d3D12CommandList is null, nameof(d3D12CommandList));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        default(InvalidOperationException).ThrowIf(
            record.ReadState() is not SubmissionState.Prepared,
            "The pending submission record is not prepared for execution.");

        FencePoint completion = device.ExecutePipelineCommandList(d3D12CommandList, copyFenceWaitValue);

        default(InvalidOperationException).ThrowIf(!record.TryMarkExecutionIssued(), "The submission could not be marked as issued.");
        default(InvalidOperationException).ThrowIf(!record.TryMarkCompletionSignaled(), "The submission completion could not be signaled.");
        default(InvalidOperationException).ThrowIf(!record.TryCommitHazards(), "The submission hazards could not be committed.");
        default(InvalidOperationException).ThrowIf(
            !completionRegistry.CommitAndPublish(host, recordIndex, completion, in retention, device.GetComputeFenceCompletedValue),
            "The submission could not be published.");

        return completion;
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

        if (!retention.ResourceUsages.IsNone)
        {
            ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(host.UsageSets.GetSetIndex(retention.ResourceUsages)));
        }
    }
}
