using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ComputeSubmissionExecutorTests
{
    private static PipelineHostRuntime Host(Device device, out DeviceRegistrationRegistry registry, int maximumPendingSubmissions = 2)
    {
        registry = new DeviceRegistrationRegistry(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);

        return registry.RegisterHost(
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1),
            maximumPendingSubmissions,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    private static int RecordAndPrepare(PipelineHostRuntime host, ulong submissionSequence, out SubmissionRetention retention)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, submissionSequence, out int index));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        _ = d3D12CommandList->Close();

        retention = new SubmissionRetention { ResourceUsages = host.GetUsageSetHandle(index) };

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));
        Assert.IsTrue(record.TryCompleteValidation());

        return index;
    }

    private static ID3D12GraphicsCommandList* GetCommandList(in SubmissionRetention retention)
    {
        SubmissionRetention copy = retention;

        return (ID3D12GraphicsCommandList*)CommandListLeaseSet.GetSegment(ref copy.CommandLists, 0).CommandList;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CompletesAFullSubmissionRoundTripOnTheComputeQueue(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            int index = RecordAndPrepare(host, 1, out SubmissionRetention retention);

            FencePoint fence = ComputeSubmissionExecutor.Submit(
                device.Get(),
                host,
                completion,
                index,
                GetCommandList(in retention),
                copyFenceWaitValue: 0,
                in retention);

            Assert.AreEqual(ComputeQueueKind.Compute, fence.Queue);
            Assert.AreNotEqual(0ul, fence.Value);
            Assert.AreEqual(1, completion.CommittedCount);
            Assert.AreEqual(1, host.CommandLists.AvailableCount);
            Assert.AreEqual(1, host.PendingRecords.AvailableCount);

            device.Get().WaitForComputeFenceValue(fence.Value);

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
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 1);

        try
        {
            CompletionRegistry completion = new();
            ulong previousFenceValue = 0;

            for (ulong i = 1; i <= 4; i++)
            {
                int index = RecordAndPrepare(host, i, out SubmissionRetention retention);

                FencePoint fence = ComputeSubmissionExecutor.Submit(
                    device.Get(),
                    host,
                    completion,
                    index,
                    GetCommandList(in retention),
                    copyFenceWaitValue: 0,
                    in retention);

                Assert.IsTrue(fence.Value > previousFenceValue);

                previousFenceValue = fence.Value;

                device.Get().WaitForComputeFenceValue(fence.Value);

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
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 2);

        try
        {
            CompletionRegistry completion = new();

            int first = RecordAndPrepare(host, 1, out SubmissionRetention firstRetention);
            FencePoint firstFence = ComputeSubmissionExecutor.Submit(
                device.Get(), host, completion, first, GetCommandList(in firstRetention), 0, in firstRetention);

            int second = RecordAndPrepare(host, 2, out SubmissionRetention secondRetention);
            FencePoint secondFence = ComputeSubmissionExecutor.Submit(
                device.Get(), host, completion, second, GetCommandList(in secondRetention), 0, in secondRetention);

            Assert.IsTrue(secondFence.Value > firstFence.Value);
            Assert.AreEqual(0, host.PendingRecords.AvailableCount);

            device.Get().WaitForComputeFenceValue(secondFence.Value);

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
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out _);

            _ = d3D12CommandList->Close();

            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => ComputeSubmissionExecutor.Submit(device.Get(), host, completion, index, d3D12CommandList, 0, default));

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
