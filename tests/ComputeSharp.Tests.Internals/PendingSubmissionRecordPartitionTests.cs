using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PendingSubmissionRecordPartitionTests
{
    private static PipelineKey Key(uint ordinal = 0)
    {
        return new PipelineKey(new HostRegistrationId(1), new PipelineOrdinal(ordinal));
    }

    private static void DriveToReturned(PendingSubmissionRecordPartition partition, int index)
    {
        ref PendingSubmissionRecord record = ref partition.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());
        Assert.IsTrue(record.TryCompleteValidation());
        Assert.IsTrue(record.TryMarkExecutionIssued());
        Assert.IsTrue(record.TryMarkCompletionSignaled());
        Assert.IsTrue(record.TryCommitHazards());
        Assert.IsTrue(record.TryCommitAndPublish(new FencePoint(ComputeQueueKind.Compute, 1), default));
        Assert.IsTrue(record.TryMarkCompletionReady());
        Assert.IsTrue(record.TryClaimForReturn());
        Assert.IsTrue(record.TryDetachRetention(out _));
        Assert.IsTrue(record.TryCompleteReturn());
    }

    [TestMethod]
    public void StartsWithEveryRecordReturned()
    {
        PendingSubmissionRecordPartition partition = new(3);

        Assert.AreEqual(3, partition.Capacity);
        Assert.AreEqual(3, partition.AvailableCount);
        Assert.IsFalse(partition.HasCheckedOutRecords);

        for (int i = 0; i < partition.Capacity; i++)
        {
            Assert.AreEqual(SubmissionState.Returned, partition.GetRecord(i).ReadState());
        }
    }

    [TestMethod]
    public void ChecksOutRealRecordsUpToTheReservedCapacity()
    {
        PendingSubmissionRecordPartition partition = new(2);

        Assert.IsTrue(partition.TryCheckout(Key(0), 1, out int first));
        Assert.IsTrue(partition.TryCheckout(Key(1), 2, out int second));
        Assert.IsFalse(partition.TryCheckout(Key(0), 3, out int rejected));

        Assert.AreEqual(-1, rejected);
        Assert.AreNotEqual(first, second);
        Assert.AreEqual(0, partition.AvailableCount);
        Assert.IsTrue(partition.HasCheckedOutRecords);

        Assert.AreEqual(SubmissionState.Reserved, partition.GetRecord(first).ReadState());
        Assert.AreEqual(Key(0), partition.GetRecord(first).Pipeline);
        Assert.AreEqual(1ul, partition.GetRecord(first).SubmissionSequence);
        Assert.AreEqual(Key(1), partition.GetRecord(second).Pipeline);
        Assert.AreEqual(2ul, partition.GetRecord(second).SubmissionSequence);
    }

    [TestMethod]
    public void MutatesTheRecordInPlace()
    {
        PendingSubmissionRecordPartition partition = new(1);

        Assert.IsTrue(partition.TryCheckout(Key(), 1, out int index));

        ref PendingSubmissionRecord record = ref partition.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());
        Assert.AreEqual(SubmissionState.Recording, partition.GetRecord(index).ReadState());
    }

    [TestMethod]
    public void ReturnsRecordsForReuseAfterTheCompleteSequence()
    {
        PendingSubmissionRecordPartition partition = new(1);

        Assert.IsTrue(partition.TryCheckout(Key(), 1, out int index));

        DriveToReturned(partition, index);

        partition.Return(index);

        Assert.AreEqual(1, partition.AvailableCount);
        Assert.IsFalse(partition.HasCheckedOutRecords);

        Assert.IsTrue(partition.TryCheckout(Key(), 2, out int reused));

        Assert.AreEqual(index, reused);
        Assert.AreEqual(2ul, partition.GetRecord(reused).SubmissionSequence);
    }

    [TestMethod]
    public void ReturnsAbortedRecords()
    {
        PendingSubmissionRecordPartition partition = new(1);

        Assert.IsTrue(partition.TryCheckout(Key(), 1, out int index));
        Assert.IsTrue(partition.GetRecord(index).TryAbort());

        partition.Return(index);

        Assert.AreEqual(1, partition.AvailableCount);
    }

    [TestMethod]
    public void RejectsReturnOfRecordsThatDidNotCompleteTheirReturn()
    {
        PendingSubmissionRecordPartition partition = new(1);

        Assert.IsTrue(partition.TryCheckout(Key(), 1, out int index));

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(index));

        Assert.IsTrue(partition.GetRecord(index).TryBeginRecording());

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(index));

        Assert.AreEqual(0, partition.AvailableCount);
    }

    [TestMethod]
    public void RejectsDuplicateAndOutOfRangeReturns()
    {
        PendingSubmissionRecordPartition partition = new(2);

        Assert.IsTrue(partition.TryCheckout(Key(), 1, out int index));
        Assert.IsTrue(partition.GetRecord(index).TryAbort());

        partition.Return(index);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => partition.Return(index));
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => partition.Return(2));
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => partition.Return(-1));
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = partition.GetRecord(2).SubmissionSequence);

        Assert.AreEqual(2, partition.AvailableCount);
    }

    [TestMethod]
    public void SupportsEmptyReservation()
    {
        PendingSubmissionRecordPartition partition = new(0);

        Assert.AreEqual(0, partition.Capacity);
        Assert.IsFalse(partition.TryCheckout(Key(), 1, out _));
        Assert.IsFalse(partition.HasCheckedOutRecords);
    }

    [TestMethod]
    public void HandsOutEveryRecordExactlyOnceUnderContention()
    {
        for (int round = 0; round < 32; round++)
        {
            PendingSubmissionRecordPartition partition = new(4);

            HashSet<int> checkedOutIndices = [];
            int successCount = 0;

            _ = Parallel.For(0, 8, iteration =>
            {
                if (!partition.TryCheckout(Key(), (ulong)iteration + 1, out int index))
                {
                    return;
                }

                _ = Interlocked.Increment(ref successCount);

                lock (checkedOutIndices)
                {
                    Assert.IsTrue(checkedOutIndices.Add(index), "The same pending record was checked out twice.");
                }
            });

            Assert.AreEqual(4, successCount);
            Assert.AreEqual(4, checkedOutIndices.Count);
            Assert.AreEqual(0, partition.AvailableCount);
        }
    }
}
