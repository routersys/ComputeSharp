using System;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Tests.Internals.Helpers;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class PipelineRecordingTests
{
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FillShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public readonly int offset;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = ThreadIds.X + this.offset;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TextureFillShader : IComputeShader
    {
        public readonly ReadWriteTexture2D<float> texture;

        public readonly float value;

        public void Execute()
        {
            this.texture[ThreadIds.XY] = this.value;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void InsertsAnUnorderedAccessBarrierBetweenDependentSubmissions(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteTexture2D<float> texture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        try
        {
            CompletionRegistry completion = new();

            Assert.AreEqual(1, SubmitTextureFill(graphicsDevice, host, completion, texture, 1, 1));
            Assert.AreEqual(2, SubmitTextureFill(graphicsDevice, host, completion, texture, 2, 2));

            Assert.AreEqual(2f, texture.ToArray()[0, 0]);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAPrologueBeyondTheDeclaredCommandListSegments(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteTexture2D<float> texture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        try
        {
            CompletionRegistry completion = new();

            Assert.AreEqual(1, SubmitTextureFill(graphicsDevice, host, completion, texture, 1, 1));

            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 2, out int index));

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            Assert.IsTrue(record.TryBeginRecording());

            SubmissionRetention retention = new() { ResourceUsages = host.GetUsageSetHandle(index) };

            RecordTextureFill(graphicsDevice, host, index, ref retention, texture, 2);
            RecordTextureFill(graphicsDevice, host, index, ref retention, texture, 3);

            Assert.AreEqual(2, retention.CommandLists.Count);

            SubmissionRetention rejected = retention;

            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => ComputeSubmissionExecutor.Submit(graphicsDevice, host, completion, index, ref rejected));

            Assert.AreEqual(SubmissionState.Recording, record.ReadState());

            ReleaseRecording(host, index, ref record, ref retention);
        }
        finally
        {
            registry.Dispose();
        }
    }

    private static void RecordTextureFill(
        GraphicsDevice graphicsDevice,
        PipelineHostRuntime host,
        int index,
        ref SubmissionRetention retention,
        ReadWriteTexture2D<float> texture,
        float value)
    {
        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        ComputeContext context = graphicsDevice.CreatePipelineComputeContext(
            d3D12CommandList,
            d3D12CommandAllocator,
            host.CreateUsageRecorder(index));

        context.For(8, 8, new TextureFillShader(texture, value));
        context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

        resourceLeases?.Release();

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));
    }

    private static void ReleaseRecording(
        PipelineHostRuntime host,
        int index,
        ref PendingSubmissionRecord record,
        ref SubmissionRetention retention)
    {
        for (int i = retention.CommandLists.Count - 1; i >= 0; i--)
        {
            host.CommandLists.Return(
                (ID3D12GraphicsCommandList*)CommandListLeaseSet.GetSegment(ref retention.CommandLists, i).CommandList,
                isCommandListClosed: true);
        }

        ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(index));

        Assert.IsTrue(record.TryAbort());

        host.ReturnPendingRecord(index);
    }

    private static int SubmitTextureFill(
        GraphicsDevice graphicsDevice,
        PipelineHostRuntime host,
        CompletionRegistry completion,
        ReadWriteTexture2D<float> texture,
        ulong submissionSequence,
        float value)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, submissionSequence, out int index));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        ComputeContext context = graphicsDevice.CreatePipelineComputeContext(
            d3D12CommandList,
            d3D12CommandAllocator,
            host.CreateUsageRecorder(index));

        context.For(8, 8, new TextureFillShader(texture, value));
        context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

        SubmissionRetention retention = new()
        {
            ResourceUsages = host.GetUsageSetHandle(index),
            ResourceLeases = resourceLeases
        };

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

        ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
            graphicsDevice, host, completion, index, ref retention);

        int segmentCount = retention.CommandLists.Count;

        if (segmentCount == 2)
        {
            Assert.AreNotEqual((nint)d3D12CommandList, CommandListLeaseSet.GetSegment(ref retention.CommandLists, 0).CommandList);
            Assert.AreEqual((nint)d3D12CommandList, CommandListLeaseSet.GetSegment(ref retention.CommandLists, 1).CommandList);
        }

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(graphicsDevice, completion));

        return segmentCount;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsAndSubmitsThroughACommandListOwnedByTheHost(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            Assert.IsTrue(record.TryBeginRecording());

            host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

            ComputeContext context = graphicsDevice.CreatePipelineComputeContext(
                d3D12CommandList,
                d3D12CommandAllocator,
                host.CreateUsageRecorder(index));

            context.For(64, new FillShader(buffer, 1));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            SubmissionRetention retention = new()
            {
                ResourceUsages = host.GetUsageSetHandle(index),
                ResourceLeases = resourceLeases
            };

            Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

            ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                graphicsDevice, host, completion, index, ref retention);

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(graphicsDevice, completion));

            int[] data = buffer.ToArray();

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(i + 1, data[i]);
            }
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ExecutesEveryRecordedCommandListSegment(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 2);

        using ReadWriteBuffer<int> first = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> second = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            Assert.IsTrue(record.TryBeginRecording());

            SubmissionRetention retention = new() { ResourceUsages = host.GetUsageSetHandle(index) };

            retention.ResourceLeases = Record(graphicsDevice, host, index, ref retention, first, 1);

            GraphicsResourceLeaseSet? secondLeases = Record(graphicsDevice, host, index, ref retention, second, 100);

            Assert.AreEqual(2, retention.CommandLists.Count);

            ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                graphicsDevice, host, completion, index, ref retention);

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(graphicsDevice, completion));

            secondLeases?.Release();

            int[] firstData = first.ToArray();
            int[] secondData = second.ToArray();

            for (int i = 0; i < firstData.Length; i++)
            {
                Assert.AreEqual(i + 1, firstData[i]);
                Assert.AreEqual(i + 100, secondData[i]);
            }
        }
        finally
        {
            registry.Dispose();
        }
    }

    private static GraphicsResourceLeaseSet? Record(
        GraphicsDevice graphicsDevice,
        PipelineHostRuntime host,
        int index,
        ref SubmissionRetention retention,
        ReadWriteBuffer<int> buffer,
        int offset)
    {
        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        ComputeContext context = graphicsDevice.CreatePipelineComputeContext(
            d3D12CommandList,
            d3D12CommandAllocator,
            host.CreateUsageRecorder(index));

        context.For(64, new FillShader(buffer, offset));
        context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

        return resourceLeases;
    }
}
