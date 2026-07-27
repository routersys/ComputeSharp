using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Tests.Internals.Helpers;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ComputeSubmissionExecutorTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void CompletesAFullSubmissionRoundTripOnTheComputeQueue(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = PipelineSubmissionSetup.Record(host, 1, out SubmissionRetention retention);

            ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                device.Get(),
                host,
                completion,
                index,
                bundleIndex: 0,
                ref retention);

            Assert.AreEqual(ComputeQueueKind.Compute, submission.Completion.Queue);
            Assert.AreNotEqual(0ul, submission.Completion.Value);
            Assert.AreEqual(1, completion.CommittedCount);
            Assert.AreEqual(1, host.CommandLists.AvailableCount);
            Assert.AreEqual(1, host.PendingRecords.AvailableCount);

            device.Get().WaitForComputeFenceValue(submission.Completion.Value);

            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));

            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(2, host.CommandLists.AvailableCount);
            Assert.AreEqual(2, host.PendingRecords.AvailableCount);
            Assert.AreEqual(SubmissionState.Returned, host.PendingRecords.GetRecord(index).ReadState());

            Assert.IsFalse(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReusesEveryReservedResourceAcrossSuccessiveSubmissions(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 1);

        try
        {
            CompletionRegistry completion = new();
            ulong previousFenceValue = 0;

            for (ulong i = 1; i <= 4; i++)
            {
                int index = PipelineSubmissionSetup.Record(host, i, out SubmissionRetention retention);

                ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                    device.Get(),
                    host,
                    completion,
                    index,
                    bundleIndex: 0,
                    ref retention);

                Assert.IsTrue(submission.Completion.Value > previousFenceValue);

                previousFenceValue = submission.Completion.Value;

                device.Get().WaitForComputeFenceValue(submission.Completion.Value);

                Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));
                Assert.AreEqual(1, host.CommandLists.AvailableCount);
                Assert.AreEqual(1, host.PendingRecords.AvailableCount);
            }
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsSubmissionsPendingUntilTheirFenceCompletes(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 2);

        try
        {
            CompletionRegistry completion = new();

            int first = PipelineSubmissionSetup.Record(host, 1, out SubmissionRetention firstRetention);
            ComputeSubmission firstSubmission = ComputeSubmissionExecutor.Submit(device.Get(), host, completion, first, bundleIndex: 0, ref firstRetention);

            int second = PipelineSubmissionSetup.Record(host, 2, out SubmissionRetention secondRetention);
            ComputeSubmission secondSubmission = ComputeSubmissionExecutor.Submit(device.Get(), host, completion, second, bundleIndex: 0, ref secondRetention);

            Assert.IsTrue(secondSubmission.Completion.Value > firstSubmission.Completion.Value);
            Assert.AreEqual(0, host.PendingRecords.AvailableCount);

            device.Get().WaitForComputeFenceValue(secondSubmission.Completion.Value);

            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));
            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));
            Assert.IsFalse(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));

            Assert.AreEqual(2, host.PendingRecords.AvailableCount);
            Assert.AreEqual(2, host.CommandLists.AvailableCount);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsSubmissionOfRecordsThatAreNotPrepared(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);

            _ = d3D12CommandList->Close();

            SubmissionRetention emptyRetention = default;

            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => ComputeSubmissionExecutor.Submit(device.Get(), host, completion, index, bundleIndex: 0, ref emptyRetention));

            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(SubmissionState.Reserved, host.PendingRecords.GetRecord(index).ReadState());

            host.CommandLists.Return(d3D12CommandList, isCommandListClosed: true);
        }
        finally
        {
            registry.Dispose();
        }
    }
}
