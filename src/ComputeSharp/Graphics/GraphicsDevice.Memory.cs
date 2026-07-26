using System;
using System.Runtime.CompilerServices;
using System.Threading;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_HEAP_TYPE;
using static ComputeSharp.Win32.D3D12_MEMORY_POOL;

#pragma warning disable CA2213

namespace ComputeSharp;

/// <inheritdoc/>
unsafe partial class GraphicsDevice
{
    /// <summary>
    /// The maximum number of admission attempts performed for a single native resource allocation.
    /// </summary>
    private const int MaximumAdmissionAttempts = 8;

    /// <summary>
    /// The <see cref="MemoryAllocationCoordinator"/> instance every native allocation of the current device goes through.
    /// </summary>
    private readonly MemoryAllocationCoordinator memoryCoordinator = new();

    /// <summary>
    /// The memory pool backing <see cref="D3D12_HEAP_TYPE_DEFAULT"/> allocations on the current device.
    /// </summary>
    private D3D12_MEMORY_POOL defaultHeapMemoryPool;

    /// <summary>
    /// The memory pool backing <see cref="D3D12_HEAP_TYPE_UPLOAD"/> allocations on the current device.
    /// </summary>
    private D3D12_MEMORY_POOL uploadHeapMemoryPool;

    /// <summary>
    /// The memory pool backing <see cref="D3D12_HEAP_TYPE_READBACK"/> allocations on the current device.
    /// </summary>
    private D3D12_MEMORY_POOL readBackHeapMemoryPool;

    /// <summary>
    /// The <see cref="MemoryBudgetObserver"/> instance tracking the budget notifications of the current device.
    /// </summary>
    private MemoryBudgetObserver? memoryBudgetObserver;

    /// <summary>
    /// Sets the memory policy every subsequent native allocation of the current device is admitted against.
    /// </summary>
    /// <param name="policy">The memory policy to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown if the previous policy is still in use, or if the broker fails.</exception>
    public void SetMemoryPolicy(in GraphicsMemoryPolicy policy)
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

        ThrowIfDeviceLost();

        GraphicsMemoryClientDescriptor descriptor = new() { AdapterLuid = Luid.ToInt64(), NodeIndex = 0 };

        this.memoryCoordinator.SetPolicy(in policy, in descriptor, IsUma);

        this.memoryBudgetObserver?.Wake();
    }

    /// <summary>
    /// Gets the memory statistics of the current device.
    /// </summary>
    /// <returns>The <see cref="GraphicsMemoryStatistics"/> value observed for the current device.</returns>
    public GraphicsMemoryStatistics GetMemoryStatistics()
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().TryGetLease(out bool isLeaseTaken);

        if (!isLeaseTaken)
        {
            return CreateMemoryStatistics(MemoryBudgetStatus.Unknown);
        }

        if (IsDeviceLost)
        {
            return CreateMemoryStatistics(MemoryBudgetStatus.DeviceLost);
        }

        GraphicsMemorySegmentStatistics local = QuerySegmentStatistics(MemoryPlacement.Local);
        GraphicsMemorySegmentStatistics nonLocal = QuerySegmentStatistics(MemoryPlacement.NonLocal);

        return new GraphicsMemoryStatistics(
            this.memoryCoordinator.Epoch,
            local,
            nonLocal,
            activeGenerationCount: 0,
            retiredGenerationCount: 0,
            managedPoolSurplusCount: 0);
    }

    /// <summary>
    /// Releases the reclaimable memory owned by the current device.
    /// </summary>
    public void TrimMemory()
    {
        using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

        ThrowIfDeviceLost();

        _ = RefreshMemoryObservations();
    }

    /// <summary>
    /// Gets whether or not the current device has been lost.
    /// </summary>
    internal bool IsDeviceLost => Volatile.Read(ref Unsafe.As<HRESULT, int>(ref this.deviceRemovedReason)) != S.S_OK;

    /// <summary>
    /// Allocates a described committed resource through the memory coordinator of the current device.
    /// </summary>
    /// <param name="description">The description of the resource to allocate.</param>
    /// <param name="d3D12Resource">The resulting <see cref="ID3D12Resource"/> object.</param>
    /// <returns>The memory accounting of the created resource.</returns>
    /// <exception cref="GraphicsMemoryAllocationException">Thrown if the allocation is not admitted, or if it fails.</exception>
    internal GraphicsMemoryAllocation AllocateCommittedResource(
        in GraphicsCommittedResourceDescription description,
        out ComPtr<ID3D12Resource> d3D12Resource)
    {
        D3D12_RESOURCE_ALLOCATION_INFO d3D12ResourceAllocationInfo = this.d3D12Device.Get()->GetResourceAllocationInfo(in description);

        GraphicsAllocationInfoStatus infoStatus = GraphicsAllocationInfo.Validate(
            d3D12ResourceAllocationInfo.SizeInBytes,
            d3D12ResourceAllocationInfo.Alignment);

        if (infoStatus is not GraphicsAllocationInfoStatus.Valid)
        {
            throw new InvalidOperationException(
                $"""The resource allocation info of the device "{this}" is not supported ({infoStatus}).""");
        }

        D3D12_HEAP_PROPERTIES d3D12HeapProperties = description.HeapProperties;

        if (!TryGetMemoryPlacement(in d3D12HeapProperties, out MemoryPlacement placement))
        {
            throw new InvalidOperationException(
                $"""The heap properties of the requested resource do not map to a memory segment of the device "{this}".""");
        }

        ulong sizeInBytes = d3D12ResourceAllocationInfo.SizeInBytes;

        for (int attempt = 0; ; attempt++)
        {
            MemoryReservationToken token = ReserveMemory(placement, sizeInBytes);

            using ComPtr<ID3D12Resource> d3D12ResourceResult = default;

            HRESULT hresult = this.d3D12Device.Get()->CreateCommittedResource(in description, out *&d3D12ResourceResult);

            if (hresult >= 0)
            {
                this.memoryCoordinator.CommitReservation(in token);

                d3D12Resource = d3D12ResourceResult.Move();

                return new GraphicsMemoryAllocation(this.memoryCoordinator, placement, sizeInBytes);
            }

            this.memoryCoordinator.AbortReservation(in token);

            NativeAllocationOutcome outcome = MemoryAllocationCoordinator.ClassifyNativeResult(hresult);

            if (outcome is NativeAllocationOutcome.OutOfMemory && attempt == 0)
            {
                _ = RefreshMemoryObservations();

                continue;
            }

            throw CreateNativeAllocationException(outcome, hresult, sizeInBytes);
        }
    }

    /// <summary>
    /// Refreshes the video memory budget observations of every active segment of the current device.
    /// </summary>
    internal void RefreshMemoryBudgetObservations()
    {
        RefreshMemoryBudgetObservation(MemoryPlacement.Local);
        RefreshMemoryBudgetObservation(MemoryPlacement.NonLocal);

        if (this.memoryCoordinator.TryClaimTrimRequest())
        {
            _ = RefreshMemoryObservations();
        }
    }

    /// <summary>
    /// Registers an event to be signaled whenever the video memory budget of the current device changes.
    /// </summary>
    /// <param name="eventHandle">The event to signal.</param>
    /// <param name="cookie">The resulting registration cookie.</param>
    /// <returns>Whether the notification could be registered.</returns>
    internal bool TryRegisterMemoryBudgetNotification(HANDLE eventHandle, out uint cookie)
    {
        cookie = 0;

        if (this.dxgiAdapter3.Get() is null)
        {
            return false;
        }

        fixed (uint* pCookie = &cookie)
        {
            return this.dxgiAdapter3.Get()->RegisterVideoMemoryBudgetChangeNotificationEvent(eventHandle, pCookie) >= 0;
        }
    }

    /// <summary>
    /// Unregisters a previously registered video memory budget notification.
    /// </summary>
    /// <param name="cookie">The registration cookie to unregister.</param>
    internal void UnregisterMemoryBudgetNotification(uint cookie)
    {
        if (this.dxgiAdapter3.Get() is not null)
        {
            this.dxgiAdapter3.Get()->UnregisterVideoMemoryBudgetChangeNotification(cookie);
        }
    }

    /// <summary>
    /// Initializes the memory topology of the current device.
    /// </summary>
    private void InitializeMemoryTopology()
    {
        this.defaultHeapMemoryPool = this.d3D12Device.Get()->GetCustomHeapProperties(1, D3D12_HEAP_TYPE_DEFAULT).MemoryPoolPreference;
        this.uploadHeapMemoryPool = this.d3D12Device.Get()->GetCustomHeapProperties(1, D3D12_HEAP_TYPE_UPLOAD).MemoryPoolPreference;
        this.readBackHeapMemoryPool = this.d3D12Device.Get()->GetCustomHeapProperties(1, D3D12_HEAP_TYPE_READBACK).MemoryPoolPreference;
        this.memoryBudgetObserver = MemoryBudgetObserver.TryCreate(this);
    }

    /// <summary>
    /// Refreshes the video memory budget observation of a single memory segment of the current device.
    /// </summary>
    /// <param name="placement">The memory segment to refresh.</param>
    private void RefreshMemoryBudgetObservation(MemoryPlacement placement)
    {
        if (!GraphicsMemorySegments.IsSegmentActive(IsUma, placement))
        {
            return;
        }

        if (TryQueryMemoryBudget(placement, out VideoMemoryBudgetSnapshot budget) is MemoryBudgetStatus.Valid)
        {
            _ = this.memoryCoordinator.ObserveBudget(placement, in budget);
        }
    }

    /// <summary>
    /// Maps the heap properties of a described resource to the memory segment backing it.
    /// </summary>
    /// <param name="d3D12HeapProperties">The heap properties to map.</param>
    /// <param name="placement">The resulting memory segment.</param>
    /// <returns>Whether <paramref name="d3D12HeapProperties"/> maps to an active memory segment.</returns>
    internal bool TryGetMemoryPlacement(in D3D12_HEAP_PROPERTIES d3D12HeapProperties, out MemoryPlacement placement)
    {
        D3D12_MEMORY_POOL d3D12MemoryPool = d3D12HeapProperties.Type switch
        {
            D3D12_HEAP_TYPE_CUSTOM => d3D12HeapProperties.MemoryPoolPreference,
            D3D12_HEAP_TYPE_DEFAULT => this.defaultHeapMemoryPool,
            D3D12_HEAP_TYPE_UPLOAD => this.uploadHeapMemoryPool,
            D3D12_HEAP_TYPE_READBACK => this.readBackHeapMemoryPool,
            _ => D3D12_MEMORY_POOL_UNKNOWN
        };

        return GraphicsMemorySegments.TryMapMemoryPool(IsUma, d3D12MemoryPool, out placement);
    }

    /// <summary>
    /// Reserves the memory of a single native allocation.
    /// </summary>
    /// <param name="placement">The memory segment to reserve from.</param>
    /// <param name="sizeInBytes">The number of bytes to reserve.</param>
    /// <returns>The <see cref="MemoryReservationToken"/> value of the admitted reservation.</returns>
    /// <exception cref="GraphicsMemoryAllocationException">Thrown if the reservation is not admitted.</exception>
    private MemoryReservationToken ReserveMemory(MemoryPlacement placement, ulong sizeInBytes)
    {
        for (int attempt = 0; attempt < MaximumAdmissionAttempts; attempt++)
        {
            using PolicyConfigurationLease lease = this.memoryCoordinator.AcquireConfigurationLease();

            MemoryAdmissionSnapshot snapshot = CreateAdmissionSnapshot(lease.Configuration);

            MemoryAdmissionStatus status = this.memoryCoordinator.TryReserve(
                placement,
                snapshot.GetSegment(placement),
                snapshot.Epoch,
                sizeInBytes,
                out MemoryReservationToken token);

            if (status is MemoryAdmissionStatus.Admitted)
            {
                return token;
            }

            if (status is not MemoryAdmissionStatus.StaleSnapshot)
            {
                throw new GraphicsMemoryAllocationException(
                    $"""The allocation of {sizeInBytes} bytes on the device "{this}" was not admitted ({status}).""");
            }
        }

        throw new GraphicsMemoryAllocationException(
            $"""The allocation of {sizeInBytes} bytes on the device "{this}" observed no stable memory snapshot.""");
    }

    /// <summary>
    /// Refreshes the memory observations of the current device outside of every runtime gate.
    /// </summary>
    /// <returns>The resulting <see cref="MemoryAdmissionSnapshot"/> value.</returns>
    private MemoryAdmissionSnapshot RefreshMemoryObservations()
    {
        using PolicyConfigurationLease lease = this.memoryCoordinator.AcquireConfigurationLease();

        return CreateAdmissionSnapshot(lease.Configuration);
    }

    /// <summary>
    /// Creates an admission snapshot for a given leased policy configuration.
    /// </summary>
    /// <param name="configuration">The leased policy configuration to observe with.</param>
    /// <returns>The resulting <see cref="MemoryAdmissionSnapshot"/> value.</returns>
    private MemoryAdmissionSnapshot CreateAdmissionSnapshot(MemoryPolicyConfiguration configuration)
    {
        SegmentObservationInput local = CreateSegmentObservation(configuration, MemoryPlacement.Local);
        SegmentObservationInput nonLocal = CreateSegmentObservation(configuration, MemoryPlacement.NonLocal);

        return this.memoryCoordinator.Observe(configuration, in local, in nonLocal, default);
    }

    /// <summary>
    /// Observes the video memory budget and the broker grant of a single memory segment.
    /// </summary>
    /// <param name="configuration">The leased policy configuration to observe with.</param>
    /// <param name="placement">The memory segment to observe.</param>
    /// <returns>The resulting <see cref="SegmentObservationInput"/> value.</returns>
    private SegmentObservationInput CreateSegmentObservation(MemoryPolicyConfiguration configuration, MemoryPlacement placement)
    {
        SegmentObservationInput input = default;

        if (!GraphicsMemorySegments.IsSegmentActive(IsUma, placement))
        {
            input.DxgiStatus = MemoryBudgetStatus.Unsupported;

            return input;
        }

        input.TopologyActive = true;
        input.DxgiStatus = TryQueryMemoryBudget(placement, out input.Dxgi);

        if (configuration.BrokerClient is IGraphicsMemoryBudgetClient client)
        {
            input.BrokerConfigured = true;
            input.HasGrant = TryGetMemoryGrant(client, placement, out input.Grant);
        }

        return input;
    }

    /// <summary>
    /// Gets the current grant of a memory segment from a registered budget client.
    /// </summary>
    /// <param name="client">The registered budget client to query.</param>
    /// <param name="placement">The memory segment to get the grant of.</param>
    /// <param name="grant">The resulting grant, if one is available.</param>
    /// <returns>Whether a grant is available for <paramref name="placement"/>.</returns>
    private static bool TryGetMemoryGrant(IGraphicsMemoryBudgetClient client, MemoryPlacement placement, out GraphicsMemoryGrant grant)
    {
        try
        {
            return client.TryGetGrant(GraphicsMemorySegments.GetSegment(placement), out grant);
        }
        catch (Exception)
        {
            grant = default;

            return false;
        }
    }

    /// <summary>
    /// Queries the memory statistics of a single memory segment of the current device.
    /// </summary>
    /// <param name="placement">The memory segment to query.</param>
    /// <returns>The resulting <see cref="GraphicsMemorySegmentStatistics"/> value.</returns>
    private GraphicsMemorySegmentStatistics QuerySegmentStatistics(MemoryPlacement placement)
    {
        if (!GraphicsMemorySegments.IsSegmentActive(IsUma, placement))
        {
            return CreateSegmentStatistics(placement, MemoryBudgetStatus.Unsupported, default);
        }

        MemoryBudgetStatus status = TryQueryMemoryBudget(placement, out VideoMemoryBudgetSnapshot budget);

        if (status is MemoryBudgetStatus.Valid)
        {
            _ = this.memoryCoordinator.ObserveBudget(placement, in budget);
        }

        return CreateSegmentStatistics(placement, status, in budget);
    }

    /// <summary>
    /// Creates the memory statistics of the current device without performing any native query.
    /// </summary>
    /// <param name="status">The budget status to report for every active segment.</param>
    /// <returns>The resulting <see cref="GraphicsMemoryStatistics"/> value.</returns>
    private GraphicsMemoryStatistics CreateMemoryStatistics(MemoryBudgetStatus status)
    {
        SegmentMemoryAccounting local = this.memoryCoordinator.GetAccounting(MemoryPlacement.Local);
        SegmentMemoryAccounting nonLocal = this.memoryCoordinator.GetAccounting(MemoryPlacement.NonLocal);

        return new GraphicsMemoryStatistics(
            this.memoryCoordinator.Epoch,
            CreateSegmentStatistics(MemoryPlacement.Local, status, in local.LastDxgiObservation),
            CreateSegmentStatistics(MemoryPlacement.NonLocal, status, in nonLocal.LastDxgiObservation),
            activeGenerationCount: 0,
            retiredGenerationCount: 0,
            managedPoolSurplusCount: 0);
    }

    /// <summary>
    /// Creates the memory statistics of a single memory segment of the current device.
    /// </summary>
    /// <param name="placement">The memory segment to report.</param>
    /// <param name="status">The budget status to report for <paramref name="placement"/> when it is active.</param>
    /// <param name="budget">The video memory budget to report for <paramref name="placement"/> when it is active.</param>
    /// <returns>The resulting <see cref="GraphicsMemorySegmentStatistics"/> value.</returns>
    private GraphicsMemorySegmentStatistics CreateSegmentStatistics(
        MemoryPlacement placement,
        MemoryBudgetStatus status,
        in VideoMemoryBudgetSnapshot budget)
    {
        GraphicsMemorySegment segment = GraphicsMemorySegments.GetSegment(placement);

        if (!GraphicsMemorySegments.IsSegmentActive(IsUma, placement))
        {
            return new GraphicsMemorySegmentStatistics(segment, MemoryBudgetStatus.Unsupported, 0, 0, 0, 0, 0);
        }

        SegmentMemoryAccounting accounting = this.memoryCoordinator.GetAccounting(placement);

        return new GraphicsMemorySegmentStatistics(
            segment,
            status,
            budget.BudgetBytes,
            budget.CurrentUsageBytes,
            accounting.OwnedBytes,
            accounting.ReservationBytes,
            accounting.RetiredPendingBytes);
    }

    /// <summary>
    /// Creates the exception matching the outcome of a failed native allocation.
    /// </summary>
    /// <param name="outcome">The classified outcome of the failed allocation.</param>
    /// <param name="hresult">The <see cref="HRESULT"/> the allocation failed with.</param>
    /// <param name="sizeInBytes">The number of bytes the failed allocation requested.</param>
    /// <returns>The <see cref="Exception"/> to throw for the failed allocation.</returns>
    private Exception CreateNativeAllocationException(NativeAllocationOutcome outcome, HRESULT hresult, ulong sizeInBytes)
    {
        if (outcome is NativeAllocationOutcome.DeviceRemoved)
        {
            hresult.Assert();
        }

        if (outcome is NativeAllocationOutcome.PlanValidationFailure)
        {
            return new InvalidOperationException(
                $"""The requested resource is not supported by the device "{this}" (0x{(uint)(int)hresult:X8}).""");
        }

        return new GraphicsMemoryAllocationException(
            $"""The allocation of {sizeInBytes} bytes on the device "{this}" failed ({outcome}, 0x{(uint)(int)hresult:X8}).""");
    }
}
