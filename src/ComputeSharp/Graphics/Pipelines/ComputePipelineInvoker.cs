using System;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe class ComputePipelineInvoker
{
    public static ComputeSubmission Submit<TInvocation>(
        DeviceRegistrationRegistry registry,
        PipelineHostRuntime host,
        int pipelineOrdinal,
        in TInvocation invocation)
        where TInvocation : struct, IComputePipelineInvocation
    {
        ReadOnlySpan<PipelineDescriptor> pipelines = host.Descriptor.Pipelines.Span;

        default(ArgumentOutOfRangeException).ThrowIfNotInRange(pipelineOrdinal, 0, pipelines.Length);

        default(InvalidOperationException).ThrowIf(!host.TryAcquireInvocation(), "The host has no concurrent invocation permit left.");

        try
        {
            return SubmitInvocation(registry, host, in pipelines[pipelineOrdinal], pipelineOrdinal, in invocation);
        }
        finally
        {
            host.ReleaseInvocation();
        }
    }

    private static ComputeSubmission SubmitInvocation<TInvocation>(
        DeviceRegistrationRegistry registry,
        PipelineHostRuntime host,
        in PipelineDescriptor pipeline,
        int pipelineOrdinal,
        in TInvocation invocation)
        where TInvocation : struct, IComputePipelineInvocation
    {
        PipelineKey key = new(host.Id, new PipelineOrdinal((uint)pipelineOrdinal));

        default(InvalidOperationException).ThrowIf(
            !host.TryCheckoutPendingRecord(key, host.CreateSubmissionSequence(), out int recordIndex),
            "The host has no pending submission record left.");

        try
        {
            default(InvalidOperationException).ThrowIf(
                !host.RecordingBundles.TryRent(out int bundleIndex),
                "The host has no recording bundle left.");

            try
            {
                return SubmitRecording(registry, host, in pipeline, recordIndex, bundleIndex, in invocation);
            }
            finally
            {
                host.RecordingBundles.Return(bundleIndex);
            }
        }
        catch
        {
            if (host.PendingRecords.GetRecord(recordIndex).TryAbort())
            {
                host.ReturnPendingRecord(recordIndex);
            }

            throw;
        }
    }

    private static ComputeSubmission SubmitRecording<TInvocation>(
        DeviceRegistrationRegistry registry,
        PipelineHostRuntime host,
        in PipelineDescriptor pipeline,
        int recordIndex,
        int bundleIndex,
        in TInvocation invocation)
        where TInvocation : struct, IComputePipelineInvocation
    {
        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        default(InvalidOperationException).ThrowIf(!record.TryBeginRecording(), "The submission record could not begin recording.");

        ComputePipelineBinder binder = new(
            host,
            host.RecordingBundles.Storage,
            ref host.RecordingBundles.GetBundle(bundleIndex));

        invocation.Bind(ref binder);

        SubmissionRetention retention = new() { ResourceUsages = host.GetUsageSetHandle(recordIndex) };

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        bool isCommandListRetained = false;

        try
        {
            ComputeContext context = host.Device.CreatePipelineComputeContext(
                d3D12CommandList,
                d3D12CommandAllocator,
                host.CreateUsageRecorder(recordIndex));

            try
            {
                invocation.Record(in context);
            }
            catch
            {
                context.EndPipelineRecording(out GraphicsResourceLeaseSet? failedLeases);

                failedLeases?.Release();

                throw;
            }

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            retention.ResourceLeases = resourceLeases;

            isCommandListRetained = retention.CommandLists.TryAdd(
                (nint)d3D12CommandList,
                (nint)d3D12CommandAllocator,
                ComputeQueueKind.Compute);

            default(InvalidOperationException).ThrowIf(!isCommandListRetained, "The submission has no command list segment left for its body.");

            ValidateContracts(host, in pipeline, recordIndex, bundleIndex);

            if (pipeline.Flags.HasFlag(PipelineFlags.InteropRoundTrip))
            {
                return ComputeSubmissionExecutor.SubmitInterop(
                    host.Device,
                    host,
                    registry.Completions,
                    recordIndex,
                    bundleIndex,
                    ref retention);
            }

            return ComputeSubmissionExecutor.Submit(
                host.Device,
                host,
                registry.Completions,
                recordIndex,
                bundleIndex,
                ref retention);
        }
        catch
        {
            if (!host.Device.IsDeviceTerminal && record.ReadState() is SubmissionState.Recording or SubmissionState.Prepared)
            {
                ResourceGenerationPinTracker.Rollback(
                    host.Device,
                    host.RecordingBundles.Storage,
                    ref host.RecordingBundles.GetBundle(bundleIndex));

                if (!isCommandListRetained)
                {
                    host.CommandLists.Return(d3D12CommandList, isCommandListClosed: true);
                }

                ReleaseFailedRecording(host, ref retention);
            }
            else
            {
                ConvertPins(host, bundleIndex, in retention);

                _ = record.TryMarkTerminalRetained();
            }

            throw;
        }
    }

    private static void ConvertPins(PipelineHostRuntime host, int bundleIndex, in SubmissionRetention retention)
    {
        ResourceGenerationPinTracker.ConvertToPendingSubmission(
            host.Device,
            host.RecordingBundles.Storage,
            ref host.RecordingBundles.GetBundle(bundleIndex),
            ComputeSubmissionExecutor.GetUsages(host, retention.ResourceUsages));
    }

    private static void ValidateContracts(
        PipelineHostRuntime host,
        in PipelineDescriptor pipeline,
        int recordIndex,
        int bundleIndex)
    {
        Span<ResourceGenerationPin> pins = ResourceGenerationPinTracker.GetPins(
            host.RecordingBundles.Storage,
            in host.RecordingBundles.GetBundle(bundleIndex));

        Span<ResourceGenerationId> boundGenerations = stackalloc ResourceGenerationId[pins.Length];

        for (int i = 0; i < pins.Length; i++)
        {
            boundGenerations[i] = pins[i].GenerationId;
        }

        ResourceContractValidationStatus status = ResourceContractValidator.Validate(
            in pipeline,
            boundGenerations,
            ComputeSubmissionExecutor.GetUsages(host, host.GetUsageSetHandle(recordIndex)));

        default(InvalidOperationException).ThrowIf(
            status is not ResourceContractValidationStatus.Valid,
            $"The recorded invocation does not match the declared resource contracts ({status}).");
    }

    private static void ReleaseFailedRecording(PipelineHostRuntime host, ref SubmissionRetention retention)
    {
        for (int i = retention.CommandLists.Count - 1; i >= 0; i--)
        {
            ref CommandListSegmentLease lease = ref CommandListLeaseSet.GetSegment(ref retention.CommandLists, i);

            if (lease.IsValid != 0)
            {
                host.CommandLists.Return((ID3D12GraphicsCommandList*)lease.CommandList, isCommandListClosed: true);
            }
        }

        retention.ResourceLeases?.Release();

        if (!retention.ResourceUsages.IsNone)
        {
            ResourceUsageTracker.ClearUsages(
                host.UsageSets.Storage,
                ref host.UsageSets.GetSet(host.UsageSets.GetSetIndex(retention.ResourceUsages)));
        }
    }
}
