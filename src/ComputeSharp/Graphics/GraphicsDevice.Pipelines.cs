using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Win32;

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
    /// Gets the completed value of the compute queue fence.
    /// </summary>
    /// <returns>The completed value of the compute queue fence.</returns>
    internal ulong GetComputeFenceCompletedValue()
    {
        return this.d3D12ComputeFence.Get()->GetCompletedValue();
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
