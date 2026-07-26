using System;
using System.Collections.Generic;
using System.Threading;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed class CompletionRegistry
{
    private readonly struct Entry(PipelineHostRuntime host, int recordIndex, ulong fenceValue)
    {
        public PipelineHostRuntime Host { get; } = host;

        public int RecordIndex { get; } = recordIndex;

        public ulong FenceValue { get; } = fenceValue;
    }

    private readonly List<Entry> committedRecords = [];

    private readonly Lock registryGate = new();

    private bool isArmRequested;

    private CompletionCoordinator? coordinator;

    public int CommittedCount
    {
        get
        {
            lock (this.registryGate)
            {
                return this.committedRecords.Count;
            }
        }
    }

    public bool IsArmRequested
    {
        get
        {
            lock (this.registryGate)
            {
                return this.isArmRequested;
            }
        }
    }

    public void AttachCoordinator(CompletionCoordinator coordinator)
    {
        default(ArgumentNullException).ThrowIfNull(coordinator);

        lock (this.registryGate)
        {
            default(InvalidOperationException).ThrowIf(this.coordinator is not null, "The completion registry already has a coordinator.");

            this.coordinator = coordinator;
        }
    }

    public bool CommitAndPublish(
        PipelineHostRuntime host,
        int recordIndex,
        FencePoint completion,
        in SubmissionRetention retention,
        Func<ulong> completedValueReader)
    {
        default(ArgumentNullException).ThrowIfNull(host);
        default(ArgumentNullException).ThrowIfNull(completedValueReader);
        default(ArgumentException).ThrowIf(completion.IsNone, nameof(completion));

        bool isWakeRequired = false;

        try
        {
            lock (this.registryGate)
            {
                bool wasEmpty = this.committedRecords.Count == 0;

                ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

                this.committedRecords.Add(new Entry(host, recordIndex, completion.Value));

                if (!record.TryCommitAndPublish(completion, in retention))
                {
                    this.committedRecords.RemoveAt(this.committedRecords.Count - 1);

                    return false;
                }

                if (completedValueReader() >= completion.Value)
                {
                    _ = record.TryMarkCompletionReady();
                }
                else if (wasEmpty)
                {
                    this.isArmRequested = true;
                }

                isWakeRequired = true;

                return true;
            }
        }
        finally
        {
            if (isWakeRequired)
            {
                this.coordinator?.Wake();
            }
        }
    }

    public bool TryGetMinimumCommittedFence(out ulong fenceValue)
    {
        lock (this.registryGate)
        {
            this.isArmRequested = false;

            ulong minimum = ulong.MaxValue;
            bool hasCommitted = false;

            foreach (Entry entry in this.committedRecords)
            {
                if (entry.Host.PendingRecords.GetRecord(entry.RecordIndex).ReadState() is not SubmissionState.Committed)
                {
                    continue;
                }

                hasCommitted = true;

                if (entry.FenceValue < minimum)
                {
                    minimum = entry.FenceValue;
                }
            }

            fenceValue = hasCommitted ? minimum : 0;

            return hasCommitted;
        }
    }

    public int PromoteCompleted(ulong completedValue)
    {
        lock (this.registryGate)
        {
            int promotedCount = 0;

            foreach (Entry entry in this.committedRecords)
            {
                if (entry.FenceValue > completedValue)
                {
                    continue;
                }

                if (entry.Host.PendingRecords.GetRecord(entry.RecordIndex).TryMarkCompletionReady())
                {
                    promotedCount++;
                }
            }

            return promotedCount;
        }
    }

    public bool TryClaimCompletionReady(
        out PipelineHostRuntime host,
        out int recordIndex,
        out SubmissionRetention retention)
    {
        lock (this.registryGate)
        {
            for (int i = 0; i < this.committedRecords.Count; i++)
            {
                Entry entry = this.committedRecords[i];

                ref PendingSubmissionRecord record = ref entry.Host.PendingRecords.GetRecord(entry.RecordIndex);

                if (!record.TryClaimForReturn())
                {
                    continue;
                }

                this.committedRecords.RemoveAt(i);

                default(InvalidOperationException).ThrowIf(
                    !record.TryDetachRetention(out retention),
                    "The claimed pending submission record has no retention to detach.");

                host = entry.Host;
                recordIndex = entry.RecordIndex;

                return true;
            }

            host = null!;
            recordIndex = -1;
            retention = default;

            return false;
        }
    }
}
