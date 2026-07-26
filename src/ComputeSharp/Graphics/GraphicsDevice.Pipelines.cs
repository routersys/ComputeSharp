using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.DXGI_MEMORY_SEGMENT_GROUP;

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
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
}
