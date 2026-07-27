using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
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
    /// The gate serializing the resource hazard snapshot and commit of every submission of the current device.
    /// </summary>
    private readonly Lock hazardGate = new();

    /// <summary>
    /// The gate protecting <see cref="completionCoordinatorEvents"/>.
    /// </summary>
    private readonly Lock completionCoordinatorEventGate = new();

    /// <summary>
    /// The events the completion coordinators of the compute queue of the current device wait on.
    /// </summary>
    private readonly List<HANDLE> completionCoordinatorEvents = [];

    /// <summary>
    /// The <see cref="DeviceRegistrationRegistry"/> instance owning every pipeline registration of the current device.
    /// </summary>
    private DeviceRegistrationRegistry? registrationRegistry;

    /// <summary>
    /// The <see cref="ResourceIdentityAllocator"/> instance every resource identity of the current device comes from.
    /// </summary>
    private readonly ResourceIdentityAllocator resourceIdentities = new();

    /// <summary>
    /// The highest completed value observed on the compute queue fence.
    /// </summary>
    private ulong observedComputeFenceCompletedValue;

    /// <summary>
    /// The highest completed value observed on the copy queue fence.
    /// </summary>
    private ulong observedCopyFenceCompletedValue;

    /// <summary>
    /// The reason the current device reached its terminal state, or <see langword="null"/> if it has not.
    /// </summary>
    private Exception? terminalException;

    /// <summary>
    /// Gets whether or not the current device allocates resources through an opaque custom allocator.
    /// </summary>
    internal bool HasOpaqueMemoryAllocator => this.allocator.Get() is not null;

    /// <summary>
    /// Gets the <see cref="ResourceIdentityAllocator"/> instance every resource identity of the current device comes from.
    /// </summary>
    /// <remarks>
    /// Resource, generation and generation set identifiers are monotonic per device, so every resource of the
    /// current device draws them from here, whether it belongs to a generated resource plan or not.
    /// </remarks>
    internal ResourceIdentityAllocator ResourceIdentities => this.resourceIdentities;

    /// <summary>
    /// Gets the gate owning the D3D12 state and the fence points of every resource generation of the current device.
    /// </summary>
    /// <remarks>
    /// Hazard snapshot, barrier recording and hazard commit of a submission run under this gate, so that the state a
    /// recorded barrier transitions from is the state the submission commits back.
    /// </remarks>
    internal Lock HazardGate => this.hazardGate;

    /// <summary>
    /// Gets whether the current device reached its terminal state.
    /// </summary>
    /// <remarks>
    /// A removed device is terminal, and so is a device whose queue, fence or identity sequence failed.
    /// </remarks>
    internal bool IsDeviceTerminal => Volatile.Read(ref this.terminalException) is not null || IsDeviceLost;

    /// <summary>
    /// Moves the current device to its terminal state.
    /// </summary>
    /// <param name="reason">The reason the current device is terminal.</param>
    /// <remarks>
    /// The first reason is the one that is kept, and every generation the device still owns is moved to
    /// <see cref="ResourceGenerationState.TerminalRetained"/>, so that only a device teardown releases it.
    /// </remarks>
    internal void MarkDeviceTerminal(Exception reason)
    {
        default(ArgumentNullException).ThrowIfNull(reason);

        if (Interlocked.CompareExchange(ref this.terminalException, reason, null) is not null)
        {
            return;
        }

        DeviceRegistrationRegistry? registry;

        lock (this.registrationRegistryGate)
        {
            registry = this.registrationRegistry;
        }

        registry?.MarkGenerationsTerminalRetained();
    }

    /// <summary>
    /// Throws if the current device reached its terminal state.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the current device has been lost.</exception>
    internal void ThrowIfDeviceTerminal()
    {
        if (Volatile.Read(ref this.terminalException) is Exception reason)
        {
            ExceptionDispatchInfo.Throw(reason);
        }

        ThrowIfDeviceLost();
    }

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
    /// Gets the structural aggregate of every host and resource set registered on the current device.
    /// </summary>
    /// <returns>The <see cref="DeviceStructuralAggregate"/> value of the current device.</returns>
    /// <remarks>
    /// A device that has never registered a host has no registry, and its aggregate is the default value.
    /// The registry is not created here, so observing the aggregate never reserves anything.
    /// </remarks>
    internal DeviceStructuralAggregate GetRegistrationAggregate()
    {
        DeviceRegistrationRegistry? registry;

        lock (this.registrationRegistryGate)
        {
            registry = this.registrationRegistry;
        }

        return registry?.Aggregate ?? default;
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
    /// Creates a completion coordinator event owned by the current device.
    /// </summary>
    /// <returns>The event the requesting completion coordinator waits on.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the event could not be created.</exception>
    /// <remarks>
    /// Arming a completion registers the event on the compute queue fence, and no D3D12 API revokes that
    /// registration, so the current device owns every coordinator event rather than the coordinator itself.
    /// Each coordinator gets its own event, as coordinators sharing one auto reset event would consume one
    /// another's wake ups.
    /// </remarks>
    internal HANDLE CreateCompletionCoordinatorEvent()
    {
        HANDLE eventHandle = Windows.CreateEventW(null, Windows.FALSE, Windows.FALSE, null);

        default(InvalidOperationException).ThrowIf(eventHandle == HANDLE.NULL, "The completion coordinator event could not be created.");

        lock (this.completionCoordinatorEventGate)
        {
            this.completionCoordinatorEvents.Add(eventHandle);
        }

        return eventHandle;
    }

    /// <summary>
    /// Closes every completion coordinator event of the current device.
    /// </summary>
    /// <remarks>
    /// This runs after the queue fences have been released, as closing an event the compute queue fence can
    /// still signal would let that signal reach a handle whose value the operating system may have reused.
    /// </remarks>
    private void DisposeCompletionCoordinatorEvents()
    {
        lock (this.completionCoordinatorEventGate)
        {
            foreach (HANDLE eventHandle in this.completionCoordinatorEvents)
            {
                _ = Windows.CloseHandle(eventHandle);
            }

            this.completionCoordinatorEvents.Clear();
        }
    }

    /// <summary>
    /// Executes the recorded segments of a generated pipeline submission on the compute queue and signals its completion.
    /// </summary>
    /// <param name="d3D12CommandLists">The closed command list segments to execute, in recorded order.</param>
    /// <param name="d3D12CopyFenceWaitValue">The manual copy queue fence value to wait on, or <c>0</c> for none.</param>
    /// <returns>The completion fence point of the submission.</returns>
    internal FencePoint ExecutePipelineCommandLists(ReadOnlySpan<nint> d3D12CommandLists, ulong d3D12CopyFenceWaitValue)
    {
        default(ArgumentException).ThrowIf(d3D12CommandLists.IsEmpty, nameof(d3D12CommandLists));
        default(ArgumentException).ThrowIf(d3D12CommandLists.Length > CommandListLeaseSet.MaximumSegmentCount, nameof(d3D12CommandLists));

        ID3D12CommandList** d3D12CommandListEntries = stackalloc ID3D12CommandList*[CommandListLeaseSet.MaximumSegmentCount];

        for (int i = 0; i < d3D12CommandLists.Length; i++)
        {
            default(ArgumentException).ThrowIf(d3D12CommandLists[i] == 0, nameof(d3D12CommandLists));

            d3D12CommandListEntries[i] = (ID3D12CommandList*)d3D12CommandLists[i];
        }

        ulong completionValue = 0;
        HRESULT hresult = S.S_OK;
        bool isSequenceExhausted = false;
        string operation = "Wait";

        lock (this.d3D12ComputeCommandQueueLock)
        {
            if (d3D12CopyFenceWaitValue > 0 && d3D12CopyFenceWaitValue > this.d3D12CopyFence.Get()->GetCompletedValue())
            {
                hresult = this.d3D12ComputeCommandQueue.Get()->Wait(this.d3D12CopyFence.Get(), d3D12CopyFenceWaitValue);
            }

            isSequenceExhausted = this.nextD3D12ComputeFenceValue == ulong.MaxValue;

            if (hresult >= 0 && !isSequenceExhausted)
            {
                this.d3D12ComputeCommandQueue.Get()->ExecuteCommandLists((uint)d3D12CommandLists.Length, d3D12CommandListEntries);

                completionValue = ++this.nextD3D12ComputeFenceValue;
                operation = "Signal";

                hresult = this.d3D12ComputeCommandQueue.Get()->Signal(this.d3D12ComputeFence.Get(), completionValue);
            }
        }

        if (isSequenceExhausted)
        {
            ThrowTerminalSequenceExhaustion("compute completion fence");
        }

        if (hresult < 0)
        {
            ThrowTerminalQueueFailure(hresult, operation);
        }

        return new FencePoint(ComputeQueueKind.Compute, completionValue);
    }

    /// <summary>
    /// Moves the current device to its terminal state for a failed compute queue call, and throws the reason.
    /// </summary>
    /// <param name="hresult">The <see cref="HRESULT"/> the call failed with.</param>
    /// <param name="operation">The name of the failed call.</param>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    [DoesNotReturn]
    private void ThrowTerminalQueueFailure(HRESULT hresult, string operation)
    {
        InvalidOperationException reason = new(
            $"""The "{operation}" call on the compute queue of the device "{this}" failed with code 0x{(uint)hresult:X8}.""");

        MarkDeviceTerminal(reason);

        throw reason;
    }

    /// <summary>
    /// Moves the current device to its terminal state for an exhausted identity sequence, and throws the reason.
    /// </summary>
    /// <param name="sequence">The name of the exhausted sequence.</param>
    /// <exception cref="InvalidOperationException">Always thrown.</exception>
    [DoesNotReturn]
    internal void ThrowTerminalSequenceExhaustion(string sequence)
    {
        InvalidOperationException reason = new(
            $"""The {sequence} sequence of the device "{this}" is exhausted.""");

        MarkDeviceTerminal(reason);

        throw reason;
    }

    /// <summary>
    /// Creates a <see cref="ComputeContext"/> instance recording into a command list owned by a compute pipeline host.
    /// </summary>
    /// <param name="d3D12CommandList">The <see cref="ID3D12GraphicsCommandList"/> object to record into.</param>
    /// <param name="d3D12CommandAllocator">The <see cref="ID3D12CommandAllocator"/> object backing the command list.</param>
    /// <param name="usageRecorder">The <see cref="ResourceUsageRecorder"/> instance the observed access of bound resources is recorded into.</param>
    /// <returns>The <see cref="ComputeContext"/> instance to record the pipeline invocation with.</returns>
    internal ComputeContext CreatePipelineComputeContext(
        ID3D12GraphicsCommandList* d3D12CommandList,
        ID3D12CommandAllocator* d3D12CommandAllocator,
        in ResourceUsageRecorder usageRecorder)
    {
        default(ArgumentNullException).ThrowIf(d3D12CommandList is null, nameof(d3D12CommandList));
        default(ArgumentNullException).ThrowIf(d3D12CommandAllocator is null, nameof(d3D12CommandAllocator));
        default(ArgumentException).ThrowIf(!usageRecorder.IsRecording, nameof(usageRecorder));

        ThrowIfDeviceLost();

        return new(this, d3D12CommandList, d3D12CommandAllocator, in usageRecorder);
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
        HRESULT hresult = this.d3D12ComputeFence.Get()->SetEventOnCompletion(fenceValue, eventHandle);

        if (hresult < 0)
        {
            ThrowTerminalQueueFailure(hresult, "SetEventOnCompletion");
        }
    }

    /// <summary>
    /// Waits for a given compute queue fence value to be reached.
    /// </summary>
    /// <param name="fenceValue">The compute queue fence value to wait for.</param>
    internal void WaitForComputeFenceValue(ulong fenceValue)
    {
        if (fenceValue > this.d3D12ComputeFence.Get()->GetCompletedValue())
        {
            HRESULT hresult = this.d3D12ComputeFence.Get()->SetEventOnCompletion(fenceValue, default);

            if (hresult < 0)
            {
                ThrowTerminalQueueFailure(hresult, "SetEventOnCompletion");
            }
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

        return IsDeviceTerminal ? ComputeSubmissionStatus.Faulted : ComputeSubmissionStatus.Pending;
    }

    /// <summary>
    /// Waits for a submission completing at a given fence point to reach its outcome.
    /// </summary>
    /// <param name="completion">The fence point the submission completes at.</param>
    internal void WaitForSubmission(FencePoint completion)
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().TryGetLease(out bool isLeaseTaken);

        if (!isLeaseTaken || IsDeviceTerminal)
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
