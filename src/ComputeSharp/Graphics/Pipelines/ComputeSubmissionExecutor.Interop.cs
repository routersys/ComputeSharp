using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe partial class ComputeSubmissionExecutor
{
    public static ComputeSubmission SubmitInterop(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        int bundleIndex,
        ref SubmissionRetention retention)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(host);
        default(ArgumentNullException).ThrowIfNull(completionRegistry);

        Span<GraphicsResourceUsageEntry> usages = GetUsages(host, retention.ResourceUsages);
        Span<int> externalUsages = stackalloc int[usages.Length];

        int externalCount = CollectExternalUsages(device, usages, externalUsages, out ComputeInteropDomain? domain);

        default(InvalidOperationException).ThrowIf(
            externalCount == 0 || domain is null,
            "The interop pipeline invocation observed no shared texture of an interop domain.");

        externalUsages = externalUsages[..externalCount];

        DomainOperationLease lease = AcquireRoundTripLease(domain, usages, externalUsages);

        try
        {
            return SubmitRoundTrip(device, host, completionRegistry, recordIndex, bundleIndex, domain, usages, externalUsages, ref retention);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static ComputeSubmission SubmitRoundTrip(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        int bundleIndex,
        ComputeInteropDomain domain,
        Span<GraphicsResourceUsageEntry> usages,
        ReadOnlySpan<int> externalUsages,
        ref SubmissionRetention retention)
    {
        bool isAcquireRequired = IsAcquireRequired(usages, externalUsages);
        ulong acquireValue = isAcquireRequired ? domain.ReserveTimelineValue() : 0;
        ulong releaseValue = domain.ReserveTimelineValue();

        if (isAcquireRequired)
        {
            try
            {
                domain.EnqueueExternalSignal(acquireValue);
            }
            catch (Exception e)
            {
                FaultExternalGenerations(usages, externalUsages);

                domain.MarkPoisoned(e);

                throw;
            }

            MarkAcquireSignalEnqueued(usages, externalUsages);
        }

        FencePoint completion = RecordAndExecute(
            device,
            host,
            recordIndex,
            bundleIndex,
            domain,
            usages,
            externalUsages,
            acquireValue,
            releaseValue,
            ref retention);

        try
        {
            domain.EnqueueExternalWait(releaseValue);
        }
        catch (Exception e)
        {
            FaultExternalGenerations(usages, externalUsages);

            Publish(device, host, completionRegistry, recordIndex, completion, in retention);

            domain.MarkPoisoned(e);

            throw;
        }

        MarkExternalAvailable(usages, externalUsages);

        Publish(device, host, completionRegistry, recordIndex, completion, in retention);

        return new ComputeSubmission(device, completion);
    }

    private static FencePoint RecordAndExecute(
        GraphicsDevice device,
        PipelineHostRuntime host,
        int recordIndex,
        int bundleIndex,
        ComputeInteropDomain domain,
        Span<GraphicsResourceUsageEntry> usages,
        ReadOnlySpan<int> externalUsages,
        ulong acquireValue,
        ulong releaseValue,
        ref SubmissionRetention retention)
    {
        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(recordIndex);

        lock (device.HazardGate)
        {
            default(InvalidOperationException).ThrowIf(
                record.ReadState() is not SubmissionState.Recording,
                "The pending submission record is not being recorded.");

            RecordPrologue(host, usages, ref retention, out ulong copyFenceWaitValue);
            RecordEpilogue(host, usages, externalUsages, ref retention);

            default(InvalidOperationException).ThrowIf(!record.TryCompleteValidation(), "The submission could not complete its validation.");

            Span<nint> d3D12CommandLists = stackalloc nint[CommandListLeaseSet.MaximumSegmentCount];

            int segmentCount = CollectCommandLists(ref retention, d3D12CommandLists);

            InteropQueueExecution execution = device.ExecuteInteropPipelineCommandLists(
                d3D12CommandLists[..segmentCount],
                copyFenceWaitValue,
                domain.SharedFence,
                acquireValue,
                releaseValue);

            if (execution.IsExecutionIssued)
            {
                default(InvalidOperationException).ThrowIf(!record.TryMarkExecutionIssued(), "The submission could not be marked as issued.");

                MarkComputeExecutionIssued(usages, externalUsages);
            }

            if (execution.IsSequenceExhausted)
            {
                device.ThrowTerminalSequenceExhaustion("compute completion fence");
            }

            if (execution.Result < 0)
            {
                device.ThrowTerminalQueueFailure(execution.Result, execution.FailedOperation);
            }

            default(InvalidOperationException).ThrowIf(!record.TryMarkCompletionSignaled(), "The submission completion could not be signaled.");

            ResourceHazardTracker.CommitResourceUsages(usages, execution.Completion, record.SubmissionSequence);

            MarkReleaseSignalEnqueued(usages, externalUsages);

            default(InvalidOperationException).ThrowIf(!record.TryCommitHazards(), "The submission hazards could not be committed.");

            ResourceGenerationPinTracker.ConvertToPendingSubmission(
                device,
                host.RecordingBundles.Storage,
                ref host.RecordingBundles.GetBundle(bundleIndex),
                usages);

            return execution.Completion;
        }
    }

    private static void Publish(
        GraphicsDevice device,
        PipelineHostRuntime host,
        CompletionRegistry completionRegistry,
        int recordIndex,
        FencePoint completion,
        in SubmissionRetention retention)
    {
        default(InvalidOperationException).ThrowIf(
            !completionRegistry.CommitAndPublish(host, recordIndex, completion, in retention, device.GetComputeFenceCompletedValue),
            "The submission could not be published.");
    }

    private static DomainOperationLease AcquireRoundTripLease(
        ComputeInteropDomain domain,
        Span<GraphicsResourceUsageEntry> usages,
        ReadOnlySpan<int> externalUsages)
    {
        ref ResourceGenerationRecord first = ref GetExternalRecord(usages, externalUsages[0]);

        DomainOperationStatus status = domain.TryAcquireOperation(
            ExternalDomainReference.PendingTransaction,
            first.Id,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease lease,
            out Exception? schedulerFailure);

        if (status is DomainOperationStatus.Acquired)
        {
            return lease;
        }

        throw new InvalidOperationException(
            $"The interop round-trip could not acquire an operation of its domain ({status}).",
            schedulerFailure);
    }

    private static int CollectExternalUsages(
        GraphicsDevice device,
        Span<GraphicsResourceUsageEntry> usages,
        Span<int> externalUsages,
        out ComputeInteropDomain? domain)
    {
        domain = null;

        int externalCount = 0;

        for (int i = 0; i < usages.Length; i++)
        {
            if (usages[i].Set.Owner is not ResourceGenerationOwner { Domain: ComputeInteropDomain owningDomain })
            {
                continue;
            }

            default(InvalidOperationException).ThrowIf(
                !ReferenceEquals(owningDomain.Device, device),
                "A shared texture of the interop pipeline invocation belongs to a domain of another device.");
            default(InvalidOperationException).ThrowIf(
                domain is not null && !ReferenceEquals(domain, owningDomain),
                "The interop pipeline invocation observed shared textures of more than one interop domain.");

            domain = owningDomain;
            externalUsages[externalCount++] = i;
        }

        return externalCount;
    }

    private static void RecordEpilogue(
        PipelineHostRuntime host,
        Span<GraphicsResourceUsageEntry> usages,
        ReadOnlySpan<int> externalUsages,
        ref SubmissionRetention retention)
    {
        Span<ResourceBarrierPlanEntry> plan = stackalloc ResourceBarrierPlanEntry[externalUsages.Length];

        int barrierCount = 0;

        for (int i = 0; i < externalUsages.Length; i++)
        {
            int usageIndex = externalUsages[i];

            if (usages[usageIndex].FinalState is TrackedResourceState.Common)
            {
                continue;
            }

            plan[barrierCount++] = new ResourceBarrierPlanEntry(
                usageIndex,
                ResourceBarrierKind.Transition,
                usages[usageIndex].FinalState,
                TrackedResourceState.Common);

            usages[usageIndex].FinalState = TrackedResourceState.Common;
        }

        if (barrierCount == 0)
        {
            return;
        }

        default(InvalidOperationException).ThrowIf(
            retention.CommandLists.Count >= host.Descriptor.Structural.MaximumCommandListSegments,
            "The submission has no declared command list segment left for its epilogue.");

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        bool isSegmentRetained = false;

        try
        {
            ResourceBarrierRecorder.RecordBarriers(d3D12CommandList, usages, plan[..barrierCount]);

            d3D12CommandList->Close().Assert();

            isSegmentRetained = retention.CommandLists.TryAdd(
                (nint)d3D12CommandList,
                (nint)d3D12CommandAllocator,
                ComputeQueueKind.Compute);

            default(InvalidOperationException).ThrowIf(!isSegmentRetained, "The submission has no command list segment left for its epilogue.");
        }
        finally
        {
            if (!isSegmentRetained)
            {
                host.CommandLists.Return(d3D12CommandList, isCommandListClosed: false);
            }
        }
    }

    private static int CollectCommandLists(ref SubmissionRetention retention, Span<nint> d3D12CommandLists)
    {
        int segmentCount = 0;

        for (int i = 0; i < retention.CommandLists.Count; i++)
        {
            ref CommandListSegmentLease lease = ref CommandListLeaseSet.GetSegment(ref retention.CommandLists, i);

            if (lease.IsValid != 0)
            {
                d3D12CommandLists[segmentCount++] = lease.CommandList;
            }
        }

        return segmentCount;
    }

    private static bool IsAcquireRequired(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        bool isAcquireRequired = false;

        for (int i = 0; i < externalUsages.Length; i++)
        {
            ExternalOwnershipState ownership = GetExternalRecord(usages, externalUsages[i]).ReadOwnership();

            default(InvalidOperationException).ThrowIf(
                ownership is not (ExternalOwnershipState.ExternalAvailable or ExternalOwnershipState.ComputeAvailable),
                $"A shared texture of the interop pipeline invocation is not available for a round-trip ({ownership}).");

            isAcquireRequired |= ownership is ExternalOwnershipState.ExternalAvailable;
        }

        return isAcquireRequired;
    }

    private static void MarkAcquireSignalEnqueued(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        for (int i = 0; i < externalUsages.Length; i++)
        {
            _ = GetExternalRecord(usages, externalUsages[i]).TryMarkAcquireSignalEnqueued();
        }
    }

    private static void MarkComputeExecutionIssued(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        for (int i = 0; i < externalUsages.Length; i++)
        {
            ref ResourceGenerationRecord record = ref GetExternalRecord(usages, externalUsages[i]);

            default(InvalidOperationException).ThrowIf(
                !record.TryMarkComputeExecutionIssued(),
                "A shared texture of the interop pipeline invocation left the round-trip before its execution.");
        }
    }

    private static void MarkReleaseSignalEnqueued(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        for (int i = 0; i < externalUsages.Length; i++)
        {
            ref ResourceGenerationRecord record = ref GetExternalRecord(usages, externalUsages[i]);

            default(InvalidOperationException).ThrowIf(
                !record.TryMarkReleaseSignalEnqueued(),
                "A shared texture of the interop pipeline invocation left the round-trip before its release.");
        }
    }

    private static void MarkExternalAvailable(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        for (int i = 0; i < externalUsages.Length; i++)
        {
            ref ResourceGenerationRecord record = ref GetExternalRecord(usages, externalUsages[i]);

            default(InvalidOperationException).ThrowIf(
                !record.TryMarkExternalAvailable(),
                "A shared texture of the interop pipeline invocation did not complete its round-trip.");
        }
    }

    private static void FaultExternalGenerations(Span<GraphicsResourceUsageEntry> usages, ReadOnlySpan<int> externalUsages)
    {
        for (int i = 0; i < externalUsages.Length; i++)
        {
            ref ResourceGenerationRecord record = ref GetExternalRecord(usages, externalUsages[i]);

            _ = record.TryMarkOwnershipFaulted();
            _ = record.TryMarkFaulted();
        }
    }

    private static ref ResourceGenerationRecord GetExternalRecord(Span<GraphicsResourceUsageEntry> usages, int usageIndex)
    {
        ref GraphicsResourceUsageEntry usage = ref usages[usageIndex];

        return ref usage.Set.Owner.GetResourceRecord(checked((int)usage.ResourceIndex));
    }
}
