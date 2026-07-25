using System;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PendingSubmissionStateMachineTests
{
    private sealed class RecordHolder
    {
        public PendingSubmissionRecord Record;
    }

    private static PipelineKey Key()
    {
        return new PipelineKey(new HostRegistrationId(1), new PipelineOrdinal(0));
    }

    private static RecordHolder ReservedRecord()
    {
        RecordHolder holder = new();

        holder.Record.State = SubmissionState.Returned;

        Assert.IsTrue(holder.Record.TryReserve(Key(), 1));

        return holder;
    }

    private static SubmissionRetention Retention()
    {
        SubmissionRetention retention = new() { ResourceUsages = UsageSetHandle.FromIndex(3) };

        _ = retention.CommandLists.TryAdd(1, 2, ComputeQueueKind.Compute);

        return retention;
    }

    [TestMethod]
    public void UsesFourByteSubmissionStateWithStableValues()
    {
        Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(SubmissionState)));
        Assert.AreEqual(0, (int)SubmissionState.Reserved);
        Assert.AreEqual(1, (int)SubmissionState.Recording);
        Assert.AreEqual(2, (int)SubmissionState.Prepared);
        Assert.AreEqual(3, (int)SubmissionState.ExecutionIssued);
        Assert.AreEqual(4, (int)SubmissionState.CompletionSignaled);
        Assert.AreEqual(5, (int)SubmissionState.HazardCommitted);
        Assert.AreEqual(6, (int)SubmissionState.Committed);
        Assert.AreEqual(7, (int)SubmissionState.CompletionReady);
        Assert.AreEqual(8, (int)SubmissionState.Returning);
        Assert.AreEqual(9, (int)SubmissionState.Returned);
        Assert.AreEqual(10, (int)SubmissionState.TerminalRetained);
    }

    [TestMethod]
    public void FollowsTheNormalSubmissionSequence()
    {
        RecordHolder holder = ReservedRecord();

        Assert.AreEqual(SubmissionState.Reserved, holder.Record.ReadState());
        Assert.AreEqual(1ul, holder.Record.SubmissionSequence);

        Assert.IsTrue(holder.Record.TryBeginRecording());
        Assert.IsTrue(holder.Record.TryCompleteValidation());
        Assert.IsTrue(holder.Record.TryMarkExecutionIssued());
        Assert.IsTrue(holder.Record.TryMarkCompletionSignaled());
        Assert.IsTrue(holder.Record.TryCommitHazards());

        FencePoint completion = new(ComputeQueueKind.Compute, 7);

        Assert.IsTrue(holder.Record.TryCommitAndPublish(completion, Retention()));
        Assert.AreEqual(SubmissionState.Committed, holder.Record.ReadState());
        Assert.AreEqual(7ul, holder.Record.Completion.Value);
        Assert.AreEqual(1, holder.Record.Retention.CommandLists.Count);

        Assert.IsTrue(holder.Record.TryMarkCompletionReady());
        Assert.IsTrue(holder.Record.TryClaimForReturn());
        Assert.IsTrue(holder.Record.TryDetachRetention(out SubmissionRetention retention));

        Assert.AreEqual(1, retention.CommandLists.Count);
        Assert.AreEqual(0, holder.Record.Retention.CommandLists.Count);

        Assert.IsTrue(holder.Record.TryCompleteReturn());
        Assert.AreEqual(SubmissionState.Returned, holder.Record.ReadState());
        Assert.AreEqual(0ul, holder.Record.SubmissionSequence);
        Assert.AreEqual(default, holder.Record.Pipeline);
    }

    [TestMethod]
    public void RejectsTransitionsOutOfOrder()
    {
        RecordHolder holder = ReservedRecord();

        Assert.IsFalse(holder.Record.TryCompleteValidation());
        Assert.IsFalse(holder.Record.TryMarkExecutionIssued());
        Assert.IsFalse(holder.Record.TryCommitHazards());
        Assert.IsFalse(holder.Record.TryCommitAndPublish(FencePoint.None, default));
        Assert.IsFalse(holder.Record.TryMarkCompletionReady());
        Assert.IsFalse(holder.Record.TryClaimForReturn());
        Assert.IsFalse(holder.Record.TryDetachRetention(out _));
        Assert.IsFalse(holder.Record.TryCompleteReturn());
        Assert.IsFalse(holder.Record.TryMarkTerminalRetained());
        Assert.AreEqual(SubmissionState.Reserved, holder.Record.ReadState());
    }

    [TestMethod]
    public void ReservesOnlyReturnedRecords()
    {
        RecordHolder holder = new();

        Assert.AreEqual(SubmissionState.Reserved, holder.Record.ReadState());
        Assert.IsFalse(holder.Record.TryReserve(Key(), 1));

        holder.Record.State = SubmissionState.Returned;

        Assert.IsTrue(holder.Record.TryReserve(Key(), 1));
        Assert.IsFalse(holder.Record.TryReserve(Key(), 2));
    }

    [TestMethod]
    public void AbortsOnlyBeforeExecutionIsIssued()
    {
        foreach (SubmissionState state in new[] { SubmissionState.Reserved, SubmissionState.Recording, SubmissionState.Prepared })
        {
            RecordHolder holder = ReservedRecord();

            holder.Record.State = state;

            Assert.IsTrue(holder.Record.TryAbort());
            Assert.AreEqual(SubmissionState.Returned, holder.Record.ReadState());
            Assert.AreEqual(0ul, holder.Record.SubmissionSequence);
            Assert.IsFalse(holder.Record.TryAbort());
        }

        RecordHolder issued = ReservedRecord();

        issued.Record.State = SubmissionState.ExecutionIssued;

        Assert.IsFalse(issued.Record.TryAbort());
        Assert.AreEqual(SubmissionState.ExecutionIssued, issued.Record.ReadState());
    }

    [TestMethod]
    public void RetainsTerminallyOnlyAfterExecutionIsIssued()
    {
        RecordHolder issued = ReservedRecord();

        issued.Record.State = SubmissionState.ExecutionIssued;

        Assert.IsTrue(issued.Record.TryMarkTerminalRetained());
        Assert.AreEqual(SubmissionState.TerminalRetained, issued.Record.ReadState());
        Assert.IsFalse(issued.Record.TryMarkTerminalRetained());

        RecordHolder signaled = ReservedRecord();

        signaled.Record.State = SubmissionState.CompletionSignaled;

        Assert.IsTrue(signaled.Record.TryMarkTerminalRetained());

        RecordHolder committed = ReservedRecord();

        committed.Record.State = SubmissionState.Committed;

        Assert.IsFalse(committed.Record.TryMarkTerminalRetained());
    }

    [TestMethod]
    public void KeepsRetentionUntilTheClaimingThreadDetachesIt()
    {
        RecordHolder holder = ReservedRecord();

        holder.Record.State = SubmissionState.HazardCommitted;

        Assert.IsTrue(holder.Record.TryCommitAndPublish(new FencePoint(ComputeQueueKind.Compute, 3), Retention()));
        Assert.IsTrue(holder.Record.TryMarkCompletionReady());

        Assert.IsFalse(holder.Record.TryDetachRetention(out _));
        Assert.AreEqual(1, holder.Record.Retention.CommandLists.Count);

        Assert.IsTrue(holder.Record.TryClaimForReturn());
        Assert.IsTrue(holder.Record.TryDetachRetention(out SubmissionRetention retention));

        Assert.AreEqual(UsageSetHandle.FromIndex(3), retention.ResourceUsages);
        Assert.IsTrue(holder.Record.Retention.ResourceUsages.IsNone);
    }

    [TestMethod]
    public void ClaimsCompletionReadyRecordExactlyOnce()
    {
        for (int round = 0; round < 64; round++)
        {
            RecordHolder holder = ReservedRecord();

            holder.Record.State = SubmissionState.CompletionReady;

            int claimCount = 0;
            int readyCount = 0;
            bool isStarted = false;

            Thread[] threads = new Thread[8];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    _ = Interlocked.Increment(ref readyCount);

                    while (!Volatile.Read(ref isStarted))
                    {
                        Thread.SpinWait(1);
                    }

                    if (holder.Record.TryClaimForReturn())
                    {
                        _ = Interlocked.Increment(ref claimCount);
                    }
                });

                threads[i].Start();
            }

            while (Volatile.Read(ref readyCount) < threads.Length)
            {
                Thread.SpinWait(1);
            }

            Volatile.Write(ref isStarted, true);

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            Assert.AreEqual(1, claimCount);
            Assert.AreEqual(SubmissionState.Returning, holder.Record.ReadState());
        }
    }

    [TestMethod]
    public void AbortsExactlyOnceUnderContention()
    {
        for (int round = 0; round < 64; round++)
        {
            RecordHolder holder = ReservedRecord();

            int abortCount = 0;

            _ = Parallel.For(0, 8, _ =>
            {
                if (holder.Record.TryAbort())
                {
                    _ = Interlocked.Increment(ref abortCount);
                }
            });

            Assert.AreEqual(1, abortCount);
            Assert.AreEqual(SubmissionState.Returned, holder.Record.ReadState());
        }
    }
}
