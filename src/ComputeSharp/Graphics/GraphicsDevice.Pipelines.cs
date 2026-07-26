using System;
using System.Threading;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;
using static ComputeSharp.Win32.DXGI_MEMORY_SEGMENT_GROUP;

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// The gate protecting the creation of <see cref="registrationRegistry"/>.
    /// </summary>
    private readonly Lock registrationRegistryGate = new();

    /// <summary>
    /// The <see cref="DeviceRegistrationRegistry"/> instance owning every pipeline registration of the current device.
    /// </summary>
    private DeviceRegistrationRegistry? registrationRegistry;

    /// <summary>
    /// The highest completed value observed on the compute queue fence.
    /// </summary>
    private ulong observedComputeFenceCompletedValue;

    /// <summary>
    /// The highest completed value observed on the copy queue fence.
    /// </summary>
    private ulong observedCopyFenceCompletedValue;

    /// <summary>
    /// Gets whether or not the current device allocates resources through an opaque custom allocator.
    /// </summary>
    internal bool HasOpaqueMemoryAllocator => this.allocator.Get() is not null;

    /// <summary>
    /// Gets the registration registry of the current device, creating it if needed.
    /// </summary>
    /// <returns>The <see cref="DeviceRegistrationRegistry"/> instance of the current device.</returns>
    /// <exception cref="NotSupportedException">Thrown if the current device uses an opaque custom allocator.</exception>
    internal DeviceRegistrationRegistry GetRegistrationRegistry()
    {
        lock (this.registrationRegistryGate)
        {
            return this.registrationRegistry ??= new DeviceRegistrationRegistry(this, D3D12_COMMAND_LIST_TYPE_COMPUTE);
        }
    }

    /// <summary>
    /// Releases the registration registry of the current device, if one was created.
    /// </summary>
    private void DisposeRegistrationRegistry()
    {
        DeviceRegistrationRegistry? registry;

        lock (this.registrationRegistryGate)
        {
            registry = this.registrationRegistry;
        }

        registry?.Dispose();
    }

    /// <summary>
    /// Executes a recorded generated pipeline command list on the compute queue and signals its completion.
    /// </summary>
    /// <param name="d3D12CommandList">The closed command list to execute.</param>
    /// <param name="d3D12CopyFenceWaitValue">The manual copy queue fence value to wait on, or <c>0</c> for none.</param>
    /// <returns>The completion fence point of the submission.</returns>
    internal FencePoint ExecutePipelineCommandList(ID3D12GraphicsCommandList* d3D12CommandList, ulong d3D12CopyFenceWaitValue)
    {
        default(ArgumentNullException).ThrowIf(d3D12CommandList is null, nameof(d3D12CommandList));

        ulong completionValue;

        lock (this.d3D12ComputeCommandQueueLock)
        {
            if (d3D12CopyFenceWaitValue > 0 && d3D12CopyFenceWaitValue > this.d3D12CopyFence.Get()->GetCompletedValue())
            {
                this.d3D12ComputeCommandQueue.Get()->Wait(this.d3D12CopyFence.Get(), d3D12CopyFenceWaitValue).Assert();
            }

            ID3D12CommandList* d3D12CommandListEntry = (ID3D12CommandList*)d3D12CommandList;

            this.d3D12ComputeCommandQueue.Get()->ExecuteCommandLists(1, &d3D12CommandListEntry);

            completionValue = ++this.nextD3D12ComputeFenceValue;

            this.d3D12ComputeCommandQueue.Get()->Signal(this.d3D12ComputeFence.Get(), completionValue).Assert();
        }

        return new FencePoint(ComputeQueueKind.Compute, completionValue);
    }

    /// <summary>
    /// Queries the current video memory budget of a given memory segment.
    /// </summary>
    /// <param name="placement">The memory segment to query the budget of.</param>
    /// <param name="budget">The resulting video memory budget snapshot.</param>
    /// <returns>The status of the queried memory budget.</returns>
    internal MemoryBudgetStatus TryQueryMemoryBudget(MemoryPlacement placement, out VideoMemoryBudgetSnapshot budget)
    {
        budget = default;

        if (!GraphicsMemorySegments.IsSegmentActive(IsUma, placement))
        {
            return MemoryBudgetStatus.Unsupported;
        }

        if (this.dxgiAdapter3.Get() is null)
        {
            return MemoryBudgetStatus.Unsupported;
        }

        DXGI_MEMORY_SEGMENT_GROUP memorySegmentGroup = placement is MemoryPlacement.Local
            ? DXGI_MEMORY_SEGMENT_GROUP_LOCAL
            : DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL;

        DXGI_QUERY_VIDEO_MEMORY_INFO videoMemoryInfo;

        HRESULT hresult = this.dxgiAdapter3.Get()->QueryVideoMemoryInfo(0, memorySegmentGroup, &videoMemoryInfo);

        if (hresult < 0)
        {
            return MemoryAllocationCoordinator.ClassifyNativeResult(hresult) is NativeAllocationOutcome.DeviceRemoved
                ? MemoryBudgetStatus.DeviceLost
                : MemoryBudgetStatus.Unknown;
        }

        budget = new VideoMemoryBudgetSnapshot
        {
            BudgetBytes = videoMemoryInfo.Budget,
            CurrentUsageBytes = videoMemoryInfo.CurrentUsage,
            AvailableForReservationBytes = videoMemoryInfo.AvailableForReservation,
            CurrentReservationBytes = videoMemoryInfo.CurrentReservation
        };

        return MemoryBudgetStatus.Valid;
    }

    /// <summary>
    /// Gets the completed value of the compute queue fence.
    /// </summary>
    /// <returns>The completed value of the compute queue fence.</returns>
    internal ulong GetComputeFenceCompletedValue()
    {
        return this.d3D12ComputeFence.Get()->GetCompletedValue();
    }

    /// <summary>
    /// Gets whether a given fence point has been reached by the queue it belongs to.
    /// </summary>
    /// <param name="fence">The fence point to observe.</param>
    /// <returns>Whether <paramref name="fence"/> has been reached.</returns>
    internal bool IsFenceCompleted(in FencePoint fence)
    {
        return fence.Queue switch
        {
            ComputeQueueKind.Compute => this.d3D12ComputeFence.Get()->GetCompletedValue() >= fence.Value,
            ComputeQueueKind.Copy => this.d3D12CopyFence.Get()->GetCompletedValue() >= fence.Value,
            _ => fence.IsNone
        };
    }

    /// <summary>
    /// Arms a given event to be signaled when the compute queue fence reaches a target value.
    /// </summary>
    /// <param name="fenceValue">The compute queue fence value to signal the event at.</param>
    /// <param name="eventHandle">The event to signal.</param>
    internal void ArmComputeFenceEvent(ulong fenceValue, HANDLE eventHandle)
    {
        this.d3D12ComputeFence.Get()->SetEventOnCompletion(fenceValue, eventHandle).Assert();
    }

    /// <summary>
    /// Waits for a given compute queue fence value to be reached.
    /// </summary>
    /// <param name="fenceValue">The compute queue fence value to wait for.</param>
    internal void WaitForComputeFenceValue(ulong fenceValue)
    {
        if (fenceValue > this.d3D12ComputeFence.Get()->GetCompletedValue())
        {
            this.d3D12ComputeFence.Get()->SetEventOnCompletion(fenceValue, default).Assert();
        }
    }

    /// <summary>
    /// Saves the completed value of every queue fence of the current device before releasing them.
    /// </summary>
    /// <remarks>
    /// The saved values let the status of an already issued submission be evaluated after the native
    /// objects of the current device have been released.
    /// </remarks>
    private void SaveFinalFenceCompletedValues()
    {
        if (this.d3D12ComputeFence.Get() is not null)
        {
            PublishObservedValue(ref this.observedComputeFenceCompletedValue, this.d3D12ComputeFence.Get()->GetCompletedValue());
        }

        if (this.d3D12CopyFence.Get() is not null)
        {
            PublishObservedValue(ref this.observedCopyFenceCompletedValue, this.d3D12CopyFence.Get()->GetCompletedValue());
        }
    }

    /// <summary>
    /// Observes the completed value of the fence of a given queue and publishes it for later evaluation.
    /// </summary>
    /// <param name="queue">The queue to observe the fence of.</param>
    private void ObserveFenceCompletedValue(ComputeQueueKind queue)
    {
        switch (queue)
        {
            case ComputeQueueKind.Compute:
                PublishObservedValue(ref this.observedComputeFenceCompletedValue, this.d3D12ComputeFence.Get()->GetCompletedValue());
                break;
            case ComputeQueueKind.Copy:
                PublishObservedValue(ref this.observedCopyFenceCompletedValue, this.d3D12CopyFence.Get()->GetCompletedValue());
                break;
        }
    }

    /// <summary>
    /// Publishes an observed fence value, keeping the highest one that has been observed so far.
    /// </summary>
    /// <param name="target">The field holding the highest observed value.</param>
    /// <param name="value">The newly observed value.</param>
    private static void PublishObservedValue(ref ulong target, ulong value)
    {
        ulong current = Volatile.Read(ref target);

        while (value > current)
        {
            ulong previous = Interlocked.CompareExchange(ref target, value, current);

            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    /// <summary>
    /// Gets the outcome of a submission completing at a given fence point.
    /// </summary>
    /// <param name="completion">The fence point the submission completes at.</param>
    /// <returns>The outcome of the submission.</returns>
    internal ComputeSubmissionStatus GetSubmissionStatus(FencePoint completion)
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().TryGetLease(out bool isLeaseTaken);

        if (isLeaseTaken)
        {
            ObserveFenceCompletedValue(completion.Queue);
        }

        if (IsFenceCompletedFromObservation(in completion))
        {
            return ComputeSubmissionStatus.Succeeded;
        }

        return IsDeviceLost ? ComputeSubmissionStatus.Faulted : ComputeSubmissionStatus.Pending;
    }

    /// <summary>
    /// Waits for a submission completing at a given fence point to reach its outcome.
    /// </summary>
    /// <param name="completion">The fence point the submission completes at.</param>
    internal void WaitForSubmission(FencePoint completion)
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().TryGetLease(out bool isLeaseTaken);

        if (!isLeaseTaken || IsDeviceLost)
        {
            return;
        }

        switch (completion.Queue)
        {
            case ComputeQueueKind.Compute:
                WaitForComputeFenceValue(completion.Value);
                break;
            case ComputeQueueKind.Copy:
                if (completion.Value > this.d3D12CopyFence.Get()->GetCompletedValue())
                {
                    this.d3D12CopyFence.Get()->SetEventOnCompletion(completion.Value, default).Assert();
                }

                break;
            default:
                return;
        }

        ObserveFenceCompletedValue(completion.Queue);
    }

    /// <summary>
    /// Gets whether a given fence point has been reached according to the published observations.
    /// </summary>
    /// <param name="fence">The fence point to evaluate.</param>
    /// <returns>Whether <paramref name="fence"/> has been reached.</returns>
    /// <remarks>
    /// The published observations only ever move forward, so a fence point that has been evaluated as
    /// reached once keeps evaluating as reached, including after the queue fences have been released.
    /// </remarks>
    private bool IsFenceCompletedFromObservation(in FencePoint fence)
    {
        return fence.Queue switch
        {
            ComputeQueueKind.Compute => Volatile.Read(ref this.observedComputeFenceCompletedValue) >= fence.Value,
            ComputeQueueKind.Copy => Volatile.Read(ref this.observedCopyFenceCompletedValue) >= fence.Value,
            _ => fence.IsNone
        };
    }
}
