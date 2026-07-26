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

        public void Execute()
        {
            this.buffer[ThreadIds.X] = ThreadIds.X + 1;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsAndSubmitsThroughACommandListOwnedByTheHost(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            CompletionRegistry completion = new();
            PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

            Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            Assert.IsTrue(record.TryBeginRecording());

            host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

            ComputeContext context = graphicsDevice.CreatePipelineComputeContext(d3D12CommandList, d3D12CommandAllocator);

            context.For(64, new FillShader(buffer));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            SubmissionRetention retention = new()
            {
                ResourceUsages = host.GetUsageSetHandle(index),
                ResourceLeases = resourceLeases
            };

            Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));
            Assert.IsTrue(record.TryCompleteValidation());

            ComputeSubmission submission = ComputeSubmissionExecutor.Submit(
                graphicsDevice, host, completion, index, d3D12CommandList, 0, in retention);

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
}
