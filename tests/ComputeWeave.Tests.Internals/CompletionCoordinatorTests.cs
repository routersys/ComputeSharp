using System.Diagnostics;
using System.Threading;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeWeave.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe partial class CompletionCoordinatorTests
{
    private const int TimeoutMilliseconds = 10_000;

    private const uint WaitFailed = 0xFFFFFFFF;

    private static PipelineHostRuntime Host(Device device, out DeviceRegistrationRegistry registry, int maximumPendingSubmissions = 2)
    {
        registry = new DeviceRegistrationRegistry(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);

        return registry.RegisterHost(
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1),
            maximumPendingSubmissions,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    private static ComputeSubmission SubmitOne(Device device, PipelineHostRuntime host, CompletionRegistry completion, ulong submissionSequence)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, submissionSequence, out int index));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        _ = d3D12CommandList->Close();

        SubmissionRetention retention = new() { ResourceUsages = host.GetUsageSetHandle(index) };

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

        return ComputeSubmissionExecutor.Submit(device.Get(), host, completion, index, bundleIndex: 0, ref retention);
    }

    private static void WaitUntilDrained(PipelineHostRuntime host, CompletionRegistry completion, CompletionCoordinator coordinator)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (completion.CommittedCount != 0 || host.PendingRecords.AvailableCount != host.PendingRecords.Capacity)
        {
            Assert.IsNull(coordinator.Failure, coordinator.Failure?.ToString());
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < TimeoutMilliseconds, "The completion coordinator did not drain the registry.");

            Thread.Sleep(1);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DrainsASubmissionWithoutAnyExternalPolling(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        try
        {
            ComputeSubmission submission = SubmitOne(device, host, completion, 1);

            Assert.AreNotEqual(0ul, submission.Completion.Value);

            WaitUntilDrained(host, completion, coordinator);

            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(host.CommandLists.Capacity, host.CommandLists.AvailableCount);
            Assert.AreEqual(host.PendingRecords.Capacity, host.PendingRecords.AvailableCount);
            Assert.IsNull(coordinator.Failure);
        }
        finally
        {
            coordinator.Dispose();
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DrainsSuccessiveSubmissionsAndRearms(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 2);
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        try
        {
            for (ulong i = 1; i <= 6; i++)
            {
                _ = SubmitOne(device, host, completion, i);

                WaitUntilDrained(host, completion, coordinator);
            }

            Assert.IsNull(coordinator.Failure);
            Assert.AreEqual(host.PendingRecords.Capacity, host.PendingRecords.AvailableCount);
        }
        finally
        {
            coordinator.Dispose();
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DrainsConcurrentSubmissions(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry, maximumPendingSubmissions: 2);
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        try
        {
            _ = SubmitOne(device, host, completion, 1);
            _ = SubmitOne(device, host, completion, 2);

            WaitUntilDrained(host, completion, coordinator);

            Assert.AreEqual(0, completion.CommittedCount);
            Assert.AreEqual(2, host.PendingRecords.AvailableCount);
            Assert.IsNull(coordinator.Failure);
        }
        finally
        {
            coordinator.Dispose();
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AdvancesTheProgressVersionWhenASubmissionIsDrained(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        try
        {
            ulong progress = coordinator.ProgressVersion;

            _ = SubmitOne(device, host, completion, 1);

            Assert.IsTrue(coordinator.TryWaitForProgress(progress));

            WaitUntilDrained(host, completion, coordinator);

            Assert.AreNotEqual(progress, coordinator.ProgressVersion);
            Assert.IsNull(coordinator.Failure);
        }
        finally
        {
            coordinator.Dispose();
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void StopsWaitingForProgressAfterDisposal(Device device)
    {
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        coordinator.Dispose();

        Assert.IsFalse(coordinator.TryWaitForProgress(coordinator.ProgressVersion));
        Assert.IsNull(coordinator.Failure);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void StopsCleanlyWithoutAnySubmission(Device device)
    {
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        coordinator.Wake();
        coordinator.Dispose();
        coordinator.Dispose();
        coordinator.Wake();

        Assert.IsNull(coordinator.Failure);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LeavesTheEventOpenAfterDisposal(Device device)
    {
        PipelineHostRuntime host = Host(device, out DeviceRegistrationRegistry registry);
        CompletionRegistry completion = new();
        CompletionCoordinator coordinator = new(device.Get(), completion);

        completion.AttachCoordinator(coordinator);

        try
        {
            _ = SubmitOne(device, host, completion, 1);

            WaitUntilDrained(host, completion, coordinator);

            coordinator.Dispose();

            Assert.AreNotEqual(WaitFailed, Windows.WaitForSingleObjectEx(coordinator.EventHandle, 0, Windows.FALSE));
        }
        finally
        {
            coordinator.Dispose();
            registry.Dispose();
        }
    }
}
