using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Executes a given command list and waits for the operation to be completed.
    /// </summary>
    /// <param name="commandList">The input <see cref="CommandList"/> to execute.</param>
    /// <param name="resourceLeases">The resource leases and usages associated with the command list.</param>
    internal void ExecuteCommandList(ref CommandList commandList, GraphicsResourceLeaseSet? resourceLeases)
    {
        if (TryExecuteTrackedComputeCommandList(ref commandList, resourceLeases, out FencePoint completion, out CommandList prologue))
        {
            try
            {
                WaitForSubmission(completion);
            }
            finally
            {
                if (prologue.IsAllocated)
                {
                    this.computeCommandListPool.Return(prologue.DetachD3D12CommandList(), prologue.DetachD3D12CommandAllocator());
                }

                this.computeCommandListPool.Return(commandList.DetachD3D12CommandList(), commandList.DetachD3D12CommandAllocator());
            }

            return;
        }

        if (TryExecuteTrackedCopyCommandList(ref commandList, resourceLeases, out completion, out CommandList handoff))
        {
            try
            {
                WaitForSubmission(completion);
            }
            finally
            {
                if (handoff.IsAllocated)
                {
                    this.computeCommandListPool.Return(handoff.DetachD3D12CommandList(), handoff.DetachD3D12CommandAllocator());
                }

                this.copyCommandListPool.Return(commandList.DetachD3D12CommandList(), commandList.DetachD3D12CommandAllocator());
            }

            return;
        }

        ref readonly ID3D12CommandListPool commandListPool = ref Unsafe.NullRef<ID3D12CommandListPool>();
        ID3D12CommandQueue* d3D12CommandQueue;
        ID3D12Fence* d3D12Fence;
        object d3D12CommandQueueLock;
        ref ulong d3D12FenceValue = ref Unsafe.NullRef<ulong>();

        // Get the target command queue, fence and pool for the list type
        switch (commandList.D3D12CommandListType)
        {
            case D3D12_COMMAND_LIST_TYPE_COMPUTE:
                commandListPool = ref this.computeCommandListPool;
                d3D12CommandQueue = this.d3D12ComputeCommandQueue.Get();
                d3D12Fence = this.d3D12ComputeFence.Get();
                d3D12CommandQueueLock = this.d3D12ComputeCommandQueueLock;
                d3D12FenceValue = ref this.nextD3D12ComputeFenceValue;
                break;
            case D3D12_COMMAND_LIST_TYPE_COPY:
                commandListPool = ref this.copyCommandListPool;
                d3D12CommandQueue = this.d3D12CopyCommandQueue.Get();
                d3D12Fence = this.d3D12CopyFence.Get();
                d3D12CommandQueueLock = this.d3D12CopyCommandQueueLock;
                d3D12FenceValue = ref this.nextD3D12CopyFenceValue;
                break;
            default: default(ArgumentException).Throw(nameof(commandList)); return;
        }

        ID3D12Fence* d3D12WaitFence = null;
        ulong d3D12WaitFenceValue = 0;

        if (commandList.D3D12CommandListType == D3D12_COMMAND_LIST_TYPE_COPY && commandList.D3D12ComputeFenceWaitValue > 0)
        {
            d3D12WaitFence = this.d3D12ComputeFence.Get();
            d3D12WaitFenceValue = commandList.D3D12ComputeFenceWaitValue;
        }

        ulong updatedFenceValue = ExecuteCommandListCore(
            ref commandList,
            d3D12CommandQueue,
            d3D12Fence,
            ref d3D12FenceValue,
            d3D12CommandQueueLock,
            d3D12WaitFence,
            d3D12WaitFenceValue);

        // If the fence value hasn't been reached, wait until the operation completes
        if (updatedFenceValue > d3D12Fence->GetCompletedValue())
        {
            d3D12Fence->SetEventOnCompletion(updatedFenceValue, default).Assert();
        }

        // Return the rented command list and command allocator so that they can be reused
        commandListPool.Return(commandList.DetachD3D12CommandList(), commandList.DetachD3D12CommandAllocator());
    }

    /// <summary>
    /// Executes a given command list and returns an awaitable for the operation to be completed.
    /// </summary>
    /// <param name="commandList">The input <see cref="ValueTask"/> to execute.</param>
    /// <param name="resourceLeases">The resource leases and usages associated with the command list.</param>
    /// <returns>The <see cref="ValueTask"/> object representing the operation to wait for.</returns>
    /// <remarks>This method is only supported for compute operations.</remarks>
    internal ValueTask ExecuteCommandListAsync(ref CommandList commandList, GraphicsResourceLeaseSet? resourceLeases)
    {
        ulong updatedFenceValue;

        if (TryExecuteTrackedComputeCommandList(ref commandList, resourceLeases, out FencePoint completion, out CommandList prologue))
        {
            updatedFenceValue = completion.Value;

            if (prologue.IsAllocated)
            {
                this.computeCommandListPool.ReturnWhenCompleted(
                    prologue.DetachD3D12CommandList(),
                    prologue.DetachD3D12CommandAllocator(),
                    updatedFenceValue,
                    null);
            }
        }
        else
        {
            updatedFenceValue = ExecuteCommandListCore(
                ref commandList,
                this.d3D12ComputeCommandQueue.Get(),
                this.d3D12ComputeFence.Get(),
                ref this.nextD3D12ComputeFenceValue,
                this.d3D12ComputeCommandQueueLock,
                d3D12WaitFence: null,
                d3D12WaitFenceValue: 0);
        }

        // If the fence value has been reached, complete synchronously
        if (updatedFenceValue <= this.d3D12ComputeFence.Get()->GetCompletedValue())
        {
            // Return the rented command list and command allocator so that they can be reused
            this.computeCommandListPool.Return(commandList.DetachD3D12CommandList(), commandList.DetachD3D12CommandAllocator());

            return ValueTask.CompletedTask;
        }

        // Setup the awaitable task
        return WaitForFenceAsync(
            updatedFenceValue,
            this,
            commandList.DetachD3D12CommandList(),
            commandList.DetachD3D12CommandAllocator()).AsValueTask();
    }

    internal void ExecuteCommandListWithoutWaiting(ref CommandList commandList, ref GraphicsResourceLeaseSet? resourceLeases)
    {
        ulong updatedFenceValue;

        if (TryExecuteTrackedComputeCommandList(ref commandList, resourceLeases, out FencePoint completion, out CommandList prologue))
        {
            updatedFenceValue = completion.Value;

            if (prologue.IsAllocated)
            {
                this.computeCommandListPool.ReturnWhenCompleted(
                    prologue.DetachD3D12CommandList(),
                    prologue.DetachD3D12CommandAllocator(),
                    updatedFenceValue,
                    null);
            }
        }
        else
        {
            updatedFenceValue = ExecuteCommandListCore(
                ref commandList,
                this.d3D12ComputeCommandQueue.Get(),
                this.d3D12ComputeFence.Get(),
                ref this.nextD3D12ComputeFenceValue,
                this.d3D12ComputeCommandQueueLock,
                d3D12WaitFence: null,
                d3D12WaitFenceValue: 0);
        }

        GraphicsResourceLeaseSet? pendingResourceLeases = resourceLeases;

        resourceLeases = null;

        pendingResourceLeases?.MarkComputeFence(updatedFenceValue);

        this.computeCommandListPool.ReturnWhenCompleted(
            commandList.DetachD3D12CommandList(),
            commandList.DetachD3D12CommandAllocator(),
            updatedFenceValue,
            pendingResourceLeases);
    }

    private bool TryExecuteTrackedComputeCommandList(
        ref CommandList commandList,
        GraphicsResourceLeaseSet? resourceLeases,
        out FencePoint completion,
        out CommandList prologue)
    {
        Span<GraphicsResourceUsageEntry> usages = resourceLeases is null
            ? default
            : resourceLeases.GetResourceUsages();

        completion = FencePoint.None;
        prologue = default;

        if (usages.IsEmpty)
        {
            return false;
        }

        if (commandList.D3D12CommandListType is not D3D12_COMMAND_LIST_TYPE_COMPUTE)
        {
            return false;
        }

        try
        {
            lock (this.HazardGate)
            {
                Span<ResourceBarrierPlanEntry> plan = stackalloc ResourceBarrierPlanEntry[usages.Length];
                int barrierCount = ResourceHazardTracker.PlanQueueDependencies(
                    usages,
                    ComputeQueueKind.Compute,
                    plan,
                    out ulong copyFenceWaitValue);
                Span<nint> d3D12CommandLists = stackalloc nint[2];
                int commandListCount = 0;

                if (barrierCount > 0)
                {
                    prologue = new CommandList(this, D3D12_COMMAND_LIST_TYPE_COMPUTE);

                    ResourceBarrierRecorder.RecordBarriers(
                        prologue.D3D12GraphicsCommandList,
                        usages,
                        plan[..barrierCount]);

                    prologue.D3D12GraphicsCommandList->Close().Assert();
                    d3D12CommandLists[commandListCount++] = (nint)prologue.D3D12GraphicsCommandList;
                }

                d3D12CommandLists[commandListCount++] = (nint)commandList.D3D12GraphicsCommandList;

                completion = ExecutePipelineCommandLists(
                    d3D12CommandLists[..commandListCount],
                    copyFenceWaitValue);

                ResourceHazardTracker.CommitResourceUsages(usages, completion, 0);
            }
        }
        catch
        {
            prologue.Dispose();
            prologue = default;

            throw;
        }

        return true;
    }

    private bool TryExecuteTrackedCopyCommandList(
        ref CommandList commandList,
        GraphicsResourceLeaseSet? resourceLeases,
        out FencePoint completion,
        out CommandList handoff)
    {
        Span<GraphicsResourceUsageEntry> usages = resourceLeases is null
            ? default
            : resourceLeases.GetResourceUsages();

        completion = FencePoint.None;
        handoff = default;

        if (usages.IsEmpty)
        {
            return false;
        }

        if (commandList.D3D12CommandListType is not D3D12_COMMAND_LIST_TYPE_COPY)
        {
            return false;
        }

        try
        {
            lock (this.HazardGate)
            {
                Span<ResourceBarrierPlanEntry> plan = stackalloc ResourceBarrierPlanEntry[usages.Length];
                int barrierCount = ResourceHazardTracker.PlanManualCopyDependencies(
                    usages,
                    plan,
                    out ulong handoffCopyFenceWaitValue,
                    out ulong copyComputeFenceWaitValue);

                if (barrierCount > 0)
                {
                    handoff = new CommandList(this, D3D12_COMMAND_LIST_TYPE_COMPUTE);

                    ResourceBarrierRecorder.RecordBarriers(
                        handoff.D3D12GraphicsCommandList,
                        usages,
                        plan[..barrierCount]);

                    handoff.D3D12GraphicsCommandList->Close().Assert();

                    Span<nint> handoffCommandLists = [(nint)handoff.D3D12GraphicsCommandList];
                    FencePoint handoffCompletion = ExecutePipelineCommandLists(
                        handoffCommandLists,
                        handoffCopyFenceWaitValue);

                    copyComputeFenceWaitValue = Math.Max(copyComputeFenceWaitValue, handoffCompletion.Value);
                }

                Span<nint> copyCommandLists = [(nint)commandList.D3D12GraphicsCommandList];

                completion = ExecuteCopyCommandLists(
                    copyCommandLists,
                    copyComputeFenceWaitValue);

                ResourceHazardTracker.CommitResourceUsages(usages, completion, 0);
            }
        }
        catch
        {
            handoff.Dispose();
            handoff = default;

            throw;
        }

        return true;
    }

    internal void WaitForResourceAccess(IGraphicsResource resource, ComputeResourceAccess access)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentException).ThrowIf(
            access is not ComputeResourceAccess.Read and
            not ComputeResourceAccess.Write and
            not ComputeResourceAccess.ReadWrite,
            nameof(access));
        default(ArgumentException).ThrowIf(resource is not IGenerationBoundResource, nameof(resource));

        IGenerationBoundResource boundResource = (IGenerationBoundResource)resource;

        default(InvalidOperationException).ThrowIf(
            !boundResource.TryGetGenerationBinding(out ResourceUsageBinding binding),
            "The bound resource carries no generation identity to track its CPU access with.");

        ref ResourceGenerationRecord record = ref binding.Set.Owner.GetResourceRecord(checked((int)binding.ResourceIndex));

        default(InvalidOperationException).ThrowIf(
            record.Id != binding.Generation,
            "The generation of the CPU-accessed resource no longer matches its binding.");

        ulong computeFenceWaitValue = 0;
        ulong copyFenceWaitValue = 0;

        Accumulate(in record.LastWrite, ref computeFenceWaitValue, ref copyFenceWaitValue);

        if (access is not ComputeResourceAccess.Read)
        {
            Accumulate(in record.LastComputeRead, ref computeFenceWaitValue, ref copyFenceWaitValue);
            Accumulate(in record.LastCopyRead, ref computeFenceWaitValue, ref copyFenceWaitValue);
        }

        if (computeFenceWaitValue > 0)
        {
            WaitForSubmission(new FencePoint(ComputeQueueKind.Compute, computeFenceWaitValue));
        }

        if (copyFenceWaitValue > 0)
        {
            WaitForSubmission(new FencePoint(ComputeQueueKind.Copy, copyFenceWaitValue));
        }

        ThrowIfDeviceTerminal();

        static void Accumulate(in FencePoint fence, ref ulong computeFenceWaitValue, ref ulong copyFenceWaitValue)
        {
            switch (fence.Queue)
            {
                case ComputeQueueKind.Compute:
                    computeFenceWaitValue = Math.Max(computeFenceWaitValue, fence.Value);
                    break;
                case ComputeQueueKind.Copy:
                    copyFenceWaitValue = Math.Max(copyFenceWaitValue, fence.Value);
                    break;
            }
        }
    }

    private static ulong ExecuteCommandListCore(
        ref CommandList commandList,
        ID3D12CommandQueue* d3D12CommandQueue,
        ID3D12Fence* d3D12Fence,
        ref ulong d3D12FenceValue,
        object d3D12CommandQueueLock,
        ID3D12Fence* d3D12WaitFence,
        ulong d3D12WaitFenceValue)
    {
        lock (d3D12CommandQueueLock)
        {
            if (d3D12WaitFence is not null && d3D12WaitFenceValue > d3D12WaitFence->GetCompletedValue())
            {
                d3D12CommandQueue->Wait(d3D12WaitFence, d3D12WaitFenceValue).Assert();
            }

            fixed (ID3D12CommandList** d3D12CommandList = &commandList.GetD3D12CommandListPinnableAddressOf())
            {
                d3D12CommandQueue->ExecuteCommandLists(1, d3D12CommandList);
            }

            ulong updatedFenceValue = ++d3D12FenceValue;

            d3D12CommandQueue->Signal(d3D12Fence, updatedFenceValue).Assert();

            return updatedFenceValue;
        }
    }

    /// <summary>
    /// A context to support <see cref="WaitForSingleObjectCallbackForWaitForFenceAsync"/>.
    /// </summary>
    private struct CallbackContext
    {
        /// <summary>
        /// The <see cref="GCHandle"/> wrapping the <see cref="WaitForFenceValueTaskSource"/> object to signal completion.
        /// </summary>
        public GCHandle WaitForFenceValueTaskSourceHandle;

        /// <summary>
        /// The <see cref="GCHandle"/> wrapping the <see cref="GraphicsDevice"/> instance in use.
        /// </summary>
        public GCHandle GraphicsDeviceHandle;

        /// <summary>
        /// The <see cref="ID3D12GraphicsCommandList"/> used to queue the operations.
        /// </summary>
        public ID3D12GraphicsCommandList* D3D12GraphicsCommandList;

        /// <summary>
        /// The <see cref="ID3D12GraphicsCommandList"/> object backing <see name="D3D12GraphicsCommandList"/>.
        /// </summary>
        public ID3D12CommandAllocator* D3D12CommandAllocator;

        /// <summary>
        /// The event handle set when the target fence value is reached.
        /// </summary>
        public HANDLE EventHandle;
    }

    /// <summary>
    /// Asynchronously waits for a target fence value to be completed.
    /// </summary>
    /// <param name="d3D12FenceValue">The target fence value to wait for.</param>
    /// <param name="device">The <see cref="GraphicsDevice"/> instance executing the operations.</param>
    /// <param name="d3D12GraphicsCommandList">The <see cref="ID3D12GraphicsCommandList"/> used to queue the operations.</param>
    /// <param name="d3D12CommandAllocator">The <see cref="ID3D12GraphicsCommandList"/> object backing <paramref name="d3D12GraphicsCommandList"/>.</param>
    /// <returns>The <see cref="WaitForFenceValueTaskSource"/> object representing the operation to wait for.</returns>
    /// <remarks>This method is only supported for compute operations.</remarks>
    private static WaitForFenceValueTaskSource WaitForFenceAsync(
        ulong d3D12FenceValue,
        GraphicsDevice device,
        ID3D12GraphicsCommandList* d3D12GraphicsCommandList,
        ID3D12CommandAllocator* d3D12CommandAllocator)
    {
        HANDLE eventHandle = Windows.CreateEventW(null, Windows.FALSE, Windows.FALSE, null);

        device.d3D12ComputeFence.Get()->SetEventOnCompletion(d3D12FenceValue, eventHandle).Assert();

        WaitForFenceValueTaskSource waitForFenceValueTaskSource = WaitForFenceValueTaskSource.Rent();
        CallbackContext* callbackContext = (CallbackContext*)NativeMemory.Alloc((nuint)sizeof(CallbackContext));

        callbackContext->WaitForFenceValueTaskSourceHandle = GCHandle.Alloc(waitForFenceValueTaskSource);
        callbackContext->GraphicsDeviceHandle = GCHandle.Alloc(device);
        callbackContext->D3D12GraphicsCommandList = d3D12GraphicsCommandList;
        callbackContext->D3D12CommandAllocator = d3D12CommandAllocator;
        callbackContext->EventHandle = eventHandle;

        HANDLE waitHandle;

        device.GetReferenceTracker().DangerousAddRef();

        int result = Windows.RegisterWaitForSingleObject(
            phNewWaitObject: &waitHandle,
            hObject: eventHandle,
            Callback: &WaitForSingleObjectCallbackForWaitForFenceAsync,
            Context: callbackContext,
            dwMilliseconds: Windows.INFINITE,
            dwFlags: 0);

        // The register is successful if the return value is nonzero
        if (result == 0)
        {
            device.GetReferenceTracker().DangerousRelease();

            NativeMemory.Free(callbackContext);

            default(Win32Exception).Throw(E.E_FAIL);
        }

        return waitForFenceValueTaskSource;
    }

    /// <summary>
    /// The callback to signal the completion of a compute pipeline.
    /// </summary>
    /// <param name="pContext">The input context.</param>
    /// <param name="timedOut">Whether the wait has timed out.</param>
    [UnmanagedCallersOnly]
    private static void WaitForSingleObjectCallbackForWaitForFenceAsync(void* pContext, byte timedOut)
    {
        CallbackContext* callbackContext = (CallbackContext*)pContext;

        WaitForFenceValueTaskSource waitForFenceValueTaskSource = Unsafe.As<WaitForFenceValueTaskSource>(callbackContext->WaitForFenceValueTaskSourceHandle.Target)!;
        GraphicsDevice device = Unsafe.As<GraphicsDevice>(callbackContext->GraphicsDeviceHandle.Target)!;
        ID3D12GraphicsCommandList* d3D12GraphicsCommandList = callbackContext->D3D12GraphicsCommandList;
        ID3D12CommandAllocator* d3D12CommandAllocator = callbackContext->D3D12CommandAllocator;
        HANDLE eventHandle = callbackContext->EventHandle;

        callbackContext->WaitForFenceValueTaskSourceHandle.Free();
        callbackContext->GraphicsDeviceHandle.Free();

        device.computeCommandListPool.Return(d3D12GraphicsCommandList, d3D12CommandAllocator);

        // Decrement the reference count that was incremented when scheduling the completion callback
        device.GetReferenceTracker().DangerousRelease();

        _ = Windows.CloseHandle(eventHandle);

        NativeMemory.Free(callbackContext);

        waitForFenceValueTaskSource.Complete();
    }

    /// <summary>
    /// A reusable <see cref="IValueTaskSource"/> type.
    /// </summary>
    private sealed class WaitForFenceValueTaskSource : IValueTaskSource
    {
        /// <summary>
        /// The shared <see cref="ConcurrentQueue{T}"/> holding the reusable <see cref="WaitForFenceValueTaskSource"/> instances.
        /// </summary>
        private static readonly ConcurrentQueue<WaitForFenceValueTaskSource> ValueTaskSourceQueue = [];

        /// <summary>
        /// The approximate count of currently enqueued instances in <see cref="ValueTaskSourceQueue"/>.
        /// </summary>
        private static volatile int queuedValueTaskSourceCount;

        /// <summary>
        /// The wrapped <see cref="ManualResetValueTaskSourceCore{TResult}"/> instance being used.
        /// </summary>
        private ManualResetValueTaskSourceCore<object?> manualResetValueTaskSource;

        /// <summary>
        /// Rents a <see cref="WaitForFenceValueTaskSource"/> instance.
        /// </summary>
        /// <returns>A <see cref="WaitForFenceValueTaskSource"/> instance to use.</returns>
        public static WaitForFenceValueTaskSource Rent()
        {
            if (ValueTaskSourceQueue.TryDequeue(out WaitForFenceValueTaskSource? valueTaskSource))
            {
                _ = Interlocked.Decrement(ref queuedValueTaskSourceCount);
            }
            else
            {
                valueTaskSource = new WaitForFenceValueTaskSource();
            }

            return valueTaskSource;
        }

        /// <summary>
        /// Creates a new <see cref="ValueTask"/> from the current instance.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> used to wait for the underlying operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask AsValueTask()
        {
            return new(this, this.manualResetValueTaskSource.Version);
        }

        /// <summary>
        /// Signals the current instance for completion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            this.manualResetValueTaskSource.SetResult(null);
        }

        /// <inheritdoc/>
        public void GetResult(short token)
        {
            _ = this.manualResetValueTaskSource.GetResult(token);

            this.manualResetValueTaskSource.Reset();

            if (Interlocked.Increment(ref queuedValueTaskSourceCount) < 16)
            {
                ValueTaskSourceQueue.Enqueue(this);
            }
            else
            {
                _ = Interlocked.Decrement(ref queuedValueTaskSourceCount);
            }
        }

        /// <inheritdoc/>
        public ValueTaskSourceStatus GetStatus(short token)
        {
            return this.manualResetValueTaskSource.GetStatus(token);
        }

        /// <inheritdoc/>
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            this.manualResetValueTaskSource.OnCompleted(continuation, state, token, flags);
        }
    }
}
