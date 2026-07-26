using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals.Helpers;

internal static unsafe class PipelineSubmissionSetup
{
    public static PipelineHostRuntime Host(
        Device device,
        out DeviceRegistrationRegistry registry,
        int maximumPendingSubmissions = 2,
        int parameterCount = 0)
    {
        registry = new DeviceRegistrationRegistry(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);

        return registry.RegisterHost(
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1, parameterCount),
            maximumPendingSubmissions,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    public static int RecordAndPrepare(PipelineHostRuntime host, ulong submissionSequence, out SubmissionRetention retention)
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

    public static ID3D12GraphicsCommandList* GetCommandList(in SubmissionRetention retention)
    {
        SubmissionRetention copy = retention;

        return (ID3D12GraphicsCommandList*)CommandListLeaseSet.GetSegment(ref copy.CommandLists, 0).CommandList;
    }
}
