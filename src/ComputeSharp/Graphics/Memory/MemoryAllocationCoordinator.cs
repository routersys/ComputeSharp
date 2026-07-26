using System;
using System.Collections.Generic;
using ComputeSharp.Win32;

namespace ComputeSharp.Memory;

internal enum NativeAllocationOutcome : byte
{
    Succeeded = 0,
    OutOfMemory = 1,
    DeviceRemoved = 2,
    PlanValidationFailure = 3,
    Fault = 4
}

internal readonly struct MemoryReservationToken(ulong value, MemoryPlacement placement, ulong bytes)
{
    public ulong Value { get; } = value;

    public MemoryPlacement Placement { get; } = placement;

    public ulong Bytes { get; } = bytes;

    public bool IsNone => Value == 0;
}

internal sealed class MemoryAllocationCoordinator
{
    private readonly object allocationGate = new();

    private readonly HashSet<ulong> liveReservations = [];

    private DeviceMemoryObservationState observation;

    private ulong epoch = 1;

    private ulong nextReservationValue;

    public ulong Epoch
    {
        get
        {
            lock (this.allocationGate)
            {
                return this.epoch;
            }
        }
    }

    public int LiveReservationCount
    {
        get
        {
            lock (this.allocationGate)
            {
                return this.liveReservations.Count;
            }
        }
    }

    public SegmentMemoryAccounting GetAccounting(MemoryPlacement placement)
    {
        lock (this.allocationGate)
        {
            return GetSegment(placement);
        }
    }

    public ulong ObserveBudget(MemoryPlacement placement, in VideoMemoryBudgetSnapshot budget)
    {
        lock (this.allocationGate)
        {
            ref SegmentMemoryAccounting segment = ref GetSegment(placement);

            if (segment.DxgiInitialized && IsSameObservation(in segment.LastDxgiObservation, in budget))
            {
                return this.epoch;
            }

            segment.DxgiInitialized = true;
            segment.LastDxgiObservation = budget;
            this.epoch = checked(this.epoch + 1);

            return this.epoch;
        }
    }

    public MemoryAdmissionStatus TryReserve(
        MemoryPlacement placement,
        in SegmentPolicySnapshot segment,
        ulong snapshotEpoch,
        ulong requestedBytes,
        out MemoryReservationToken token)
    {
        token = default;

        lock (this.allocationGate)
        {
            if (snapshotEpoch != this.epoch)
            {
                return MemoryAdmissionStatus.StaleSnapshot;
            }

            ref SegmentMemoryAccounting accounting = ref GetSegment(placement);

            MemoryAdmissionStatus status = MemoryAdmission.Evaluate(in segment, in accounting, requestedBytes);

            if (status is not MemoryAdmissionStatus.Admitted)
            {
                return status;
            }

            ulong reservationBytes = accounting.ReservationBytes + requestedBytes;

            if (reservationBytes < accounting.ReservationBytes)
            {
                return MemoryAdmissionStatus.ArithmeticOverflow;
            }

            accounting.ReservationBytes = reservationBytes;

            ulong value = checked(this.nextReservationValue + 1);

            this.nextReservationValue = value;

            _ = this.liveReservations.Add(value);

            token = new MemoryReservationToken(value, placement, requestedBytes);

            return MemoryAdmissionStatus.Admitted;
        }
    }

    public void CommitReservation(in MemoryReservationToken token)
    {
        lock (this.allocationGate)
        {
            ref SegmentMemoryAccounting accounting = ref ClaimReservation(in token);

            ulong ownedBytes = accounting.OwnedBytes + token.Bytes;

            default(InvalidOperationException).ThrowIf(ownedBytes < accounting.OwnedBytes, "The owned memory accounting overflowed.");

            accounting.OwnedBytes = ownedBytes;
            accounting.ReservationBytes -= token.Bytes;
        }
    }

    public void AbortReservation(in MemoryReservationToken token)
    {
        lock (this.allocationGate)
        {
            ref SegmentMemoryAccounting accounting = ref ClaimReservation(in token);

            accounting.ReservationBytes -= token.Bytes;
        }
    }

    public void ReleaseOwned(MemoryPlacement placement, ulong bytes)
    {
        lock (this.allocationGate)
        {
            ref SegmentMemoryAccounting accounting = ref GetSegment(placement);

            default(InvalidOperationException).ThrowIf(bytes > accounting.OwnedBytes, "The owned memory accounting is below the released bytes.");

            accounting.OwnedBytes -= bytes;
        }
    }

    public static NativeAllocationOutcome ClassifyNativeResult(HRESULT hresult)
    {
        if (hresult >= 0)
        {
            return NativeAllocationOutcome.Succeeded;
        }

        int value = hresult;

        if (value == E.E_OUTOFMEMORY)
        {
            return NativeAllocationOutcome.OutOfMemory;
        }

        if (value == DXGI.DXGI_ERROR_DEVICE_REMOVED || value == DXGI.DXGI_ERROR_DEVICE_RESET)
        {
            return NativeAllocationOutcome.DeviceRemoved;
        }

        if (value == E.E_INVALIDARG || value == E.E_NOTIMPL)
        {
            return NativeAllocationOutcome.PlanValidationFailure;
        }

        return NativeAllocationOutcome.Fault;
    }

    private ref SegmentMemoryAccounting ClaimReservation(in MemoryReservationToken token)
    {
        default(ArgumentException).ThrowIf(token.IsNone, nameof(token));
        default(InvalidOperationException).ThrowIf(!this.liveReservations.Remove(token.Value), "The memory reservation is not live.");

        ref SegmentMemoryAccounting accounting = ref GetSegment(token.Placement);

        default(InvalidOperationException).ThrowIf(
            token.Bytes > accounting.ReservationBytes,
            "The pending memory accounting is below the reserved bytes.");

        return ref accounting;
    }

    private ref SegmentMemoryAccounting GetSegment(MemoryPlacement placement)
    {
        return ref placement is MemoryPlacement.Local ? ref this.observation.Local : ref this.observation.NonLocal;
    }

    private static bool IsSameObservation(in VideoMemoryBudgetSnapshot left, in VideoMemoryBudgetSnapshot right)
    {
        return left.BudgetBytes == right.BudgetBytes &&
            left.CurrentUsageBytes == right.CurrentUsageBytes &&
            left.AvailableForReservationBytes == right.AvailableForReservationBytes &&
            left.CurrentReservationBytes == right.CurrentReservationBytes;
    }
}
