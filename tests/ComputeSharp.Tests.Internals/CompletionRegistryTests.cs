using System;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class CompletionRegistryTests
{
    private static PipelineHostRuntime Host(Device device, out DeviceRegistrationRegistry registry, int maximumPendingSubmissions = 4)
    {
        registry = new DeviceRegistrationRegistry(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);

        return registry.RegisterHost(
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1),
            maximumPendingSubmissions,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    private static int PreparedRecord(PipelineHostRuntime host, ulong submissionSequence)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, submissionSequence, out int index));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());
        Assert.IsTrue(record.TryCompleteValidation());
        Assert.IsTrue(record.TryMarkExecutionIssued());
        Assert.IsTrue(record.TryMarkCompletionSignaled());
        Assert.IsTrue(record.TryCommitHazards());

        return index;
    }

    private static SubmissionRetention Retention(PipelineHostRuntime host, int recordIndex)
    {
        return new SubmissionRetention { ResourceUsages = host.GetUsageSetHandle(recordIndex) };
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ArmsTheCoordinatorWhenPublishingIntoAnEmptyRegistry(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = PreparedRecord(host, 1);

            Assert.IsTrue(completion.CommitAndPublish(host, index, new FencePoint(ComputeQueueKind.Compute, 5), Retention(host, index), static () => 0));

            Assert.AreEqual(1, completion.CommittedCount);
            Assert.IsTrue(completion.IsArmRequested);
            Assert.AreEqual(SubmissionState.Committed, host.PendingRecords.GetRecord(index).ReadState());

            int second = PreparedRecord(host, 2);

            Assert.IsTrue(completion.TryGetMinimumCommittedFence(out ulong minimum));
            Assert.AreEqual(5ul, minimum);
            Assert.IsFalse(completion.IsArmRequested);

            Assert.IsTrue(completion.CommitAndPublish(host, second, new FencePoint(ComputeQueueKind.Compute, 9), Retention(host, second), static () => 0));

            Assert.IsFalse(completion.IsArmRequested);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PromotesImmediatelyWhenCompletionLandedBeforePublish(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = PreparedRecord(host, 1);

            Assert.IsTrue(completion.CommitAndPublish(host, index, new FencePoint(ComputeQueueKind.Compute, 5), Retention(host, index), static () => 7));

            Assert.AreEqual(SubmissionState.CompletionReady, host.PendingRecords.GetRecord(index).ReadState());
            Assert.IsFalse(completion.IsArmRequested);
            Assert.IsFalse(completion.TryGetMinimumCommittedFence(out _));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PromotesOnlyRecordsAtOrBelowTheCompletedValue(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int first = PreparedRecord(host, 1);
            int second = PreparedRecord(host, 2);

            Assert.IsTrue(completion.CommitAndPublish(host, first, new FencePoint(ComputeQueueKind.Compute, 4), Retention(host, first), static () => 0));
            Assert.IsTrue(completion.CommitAndPublish(host, second, new FencePoint(ComputeQueueKind.Compute, 8), Retention(host, second), static () => 0));

            Assert.AreEqual(1, completion.PromoteCompleted(4));
            Assert.AreEqual(0, completion.PromoteCompleted(4));

            Assert.AreEqual(SubmissionState.CompletionReady, host.PendingRecords.GetRecord(first).ReadState());
            Assert.AreEqual(SubmissionState.Committed, host.PendingRecords.GetRecord(second).ReadState());

            Assert.IsTrue(completion.TryGetMinimumCommittedFence(out ulong minimum));
            Assert.AreEqual(8ul, minimum);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClaimsAndReturnsEveryCompletedRecord(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = PreparedRecord(host, 1);
            UsageSetHandle usages = host.GetUsageSetHandle(index);

            Assert.IsTrue(completion.CommitAndPublish(host, index, new FencePoint(ComputeQueueKind.Compute, 2), Retention(host, index), static () => 0));
            Assert.IsFalse(completion.TryClaimCompletionReady(out _, out _, out _));

            _ = completion.PromoteCompleted(2);

            Assert.IsTrue(completion.TryClaimCompletionReady(out PipelineHostRuntime claimedHost, out int claimedIndex, out SubmissionRetention retention));

            Assert.AreSame(host, claimedHost);
            Assert.AreEqual(index, claimedIndex);
            Assert.AreEqual(usages, retention.ResourceUsages);
            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(SubmissionState.Returning, host.PendingRecords.GetRecord(index).ReadState());
            Assert.IsTrue(host.PendingRecords.GetRecord(index).Retention.ResourceUsages.IsNone);

            Assert.IsTrue(host.PendingRecords.GetRecord(index).TryCompleteReturn());

            host.ReturnPendingRecord(index);

            Assert.AreEqual(host.PendingRecords.Capacity, host.PendingRecords.AvailableCount);
            Assert.IsFalse(completion.TryClaimCompletionReady(out _, out _, out _));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClaimsEachCompletedRecordExactlyOnceUnderContention(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 4);

        try
        {
            for (int round = 0; round < 16; round++)
            {
                CompletionRegistry completion = new();

                for (int i = 0; i < 4; i++)
                {
                    int index = PreparedRecord(host, (ulong)i + 1);

                    Assert.IsTrue(completion.CommitAndPublish(
                        host,
                        index,
                        new FencePoint(ComputeQueueKind.Compute, (ulong)i + 1),
                        Retention(host, index),
                        static () => 0));
                }

                _ = completion.PromoteCompleted(4);

                int claimCount = 0;

                _ = Parallel.For(0, 8, _ =>
                {
                    while (completion.TryClaimCompletionReady(out PipelineHostRuntime claimedHost, out int claimedIndex, out SubmissionRetention _))
                    {
                        _ = Interlocked.Increment(ref claimCount);

                        Assert.IsTrue(claimedHost.PendingRecords.GetRecord(claimedIndex).TryCompleteReturn());

                        claimedHost.ReturnPendingRecord(claimedIndex);
                    }
                });

                Assert.AreEqual(4, claimCount);
                Assert.AreEqual(0, completion.CommittedCount);
                Assert.AreEqual(4, host.PendingRecords.AvailableCount);
            }
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsPublishOfRecordsThatDidNotCommitHazards(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            Assert.IsFalse(completion.CommitAndPublish(host, index, new FencePoint(ComputeQueueKind.Compute, 1), default, static () => 0));
            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(SubmissionState.Reserved, host.PendingRecords.GetRecord(index).ReadState());

            _ = Assert.ThrowsExactly<ArgumentException>(
                () => completion.CommitAndPublish(host, index, FencePoint.None, default, static () => 0));
        }
        finally
        {
            registry.Dispose();
        }
    }
}
