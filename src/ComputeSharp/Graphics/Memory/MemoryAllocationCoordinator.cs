using System;
using System.Collections.Generic;
using System.Threading;
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

    private readonly object policyGate = new();

    private readonly HashSet<ulong> liveReservations = [];

    private DeviceMemoryObservationState observation;

    private MemoryPolicyState policy;

    private ulong nextReservationValue;

    public MemoryAllocationCoordinator()
    {
        this.policy.NextConfigurationVersion = 2;
        this.policy.Epoch = 1;
        this.policy.Active = new MemoryPolicyConfiguration { ConfigurationVersion = 1, State = MemoryPolicyConfigurationState.Active };
    }

    public ulong Epoch
    {
        get
        {
            lock (this.allocationGate)
            {
                return this.policy.Epoch;
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

    public bool HasRetiredConfiguration
    {
        get
        {
            lock (this.policyGate)
            {
                return this.policy.Retired is not null;
            }
        }
    }

    public PolicyConfigurationLease AcquireConfigurationLease()
    {
        lock (this.policyGate)
        {
            MemoryPolicyConfiguration configuration = this.policy.Active;

            default(InvalidOperationException).ThrowIf(
                Interlocked.Increment(ref configuration.LeaseCount) <= 0,
                "The memory policy configuration lease count overflowed.");

            return new PolicyConfigurationLease(this, configuration);
        }
    }

    public void ReleaseConfigurationLease(MemoryPolicyConfiguration configuration)
    {
        if (Interlocked.Decrement(ref configuration.LeaseCount) != 0)
        {
            return;
        }

        IGraphicsMemoryBudgetClient? client;

        lock (this.policyGate)
        {
            client = TryClaimRetiredConfiguration(configuration);
        }

        client?.Dispose();
    }

    public void SetPolicy(in GraphicsMemoryPolicy policy, in GraphicsMemoryClientDescriptor descriptor, bool isUma)
    {
        ulong expectedConfigurationVersion;

        lock (this.policyGate)
        {
            expectedConfigurationVersion = this.policy.Active.ConfigurationVersion;
        }

        IGraphicsMemoryBudgetClient? client = null;

        if (policy.BudgetBroker is IGraphicsMemoryBudgetBroker broker)
        {
            try
            {
                client = broker.RegisterClient(in descriptor);

                default(InvalidOperationException).ThrowIf(client is null, "The memory budget broker returned no client.");

                ValidateInitialGrants(client, isUma);
            }
            catch
            {
                client?.Dispose();

                throw;
            }
        }

        CommitPolicy(in policy, client, expectedConfigurationVersion);
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
            MergeBudgetObservation(placement, in budget);

            return this.policy.Epoch;
        }
    }

    public MemoryAdmissionSnapshot Observe(
        MemoryPolicyConfiguration configuration,
        in SegmentObservationInput local,
        in SegmentObservationInput nonLocal,
        in DeviceStructuralAggregate structural)
    {
        lock (this.allocationGate)
        {
            SegmentPolicySnapshot localSnapshot = MergeSegmentObservation(configuration, MemoryPlacement.Local, in local);
            SegmentPolicySnapshot nonLocalSnapshot = MergeSegmentObservation(configuration, MemoryPlacement.NonLocal, in nonLocal);

            return new MemoryAdmissionSnapshot(this.policy.Epoch, localSnapshot, nonLocalSnapshot, structural);
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
            if (snapshotEpoch != this.policy.Epoch)
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

    private static void ValidateInitialGrants(IGraphicsMemoryBudgetClient client, bool isUma)
    {
        ValidateInitialGrant(client, isUma, MemoryPlacement.Local);
        ValidateInitialGrant(client, isUma, MemoryPlacement.NonLocal);
    }

    private static void ValidateInitialGrant(IGraphicsMemoryBudgetClient client, bool isUma, MemoryPlacement placement)
    {
        if (!GraphicsMemorySegments.IsSegmentActive(isUma, placement))
        {
            return;
        }

        default(InvalidOperationException).ThrowIf(
            !client.TryGetGrant(GraphicsMemorySegments.GetSegment(placement), out _),
            "The memory budget broker granted no memory for an active segment.");
    }

    private void CommitPolicy(in GraphicsMemoryPolicy policy, IGraphicsMemoryBudgetClient? client, ulong expectedConfigurationVersion)
    {
        MemoryPolicyConfiguration configuration = new()
        {
            State = MemoryPolicyConfigurationState.Active,
            BrokerClient = client,
            LocalOwnedHardLimitBytes = policy.LocalOwnedHardLimitBytes,
            NonLocalOwnedHardLimitBytes = policy.NonLocalOwnedHardLimitBytes
        };

        IGraphicsMemoryBudgetClient? reclaimedClient = null;
        IGraphicsMemoryBudgetClient? retiredClient = null;
        bool isPublished = false;

        try
        {
            lock (this.policyGate)
            {
                reclaimedClient = TryClaimRetiredConfiguration(this.policy.Retired);

                MemoryPolicyConfiguration active = this.policy.Active;

                default(InvalidOperationException).ThrowIf(
                    active.ConfigurationVersion != expectedConfigurationVersion,
                    "The memory policy was replaced concurrently.");

                default(InvalidOperationException).ThrowIf(
                    this.policy.Retired is not null,
                    "The retired memory policy configuration is still leased.");

                default(InvalidOperationException).ThrowIf(
                    client is not null && (ReferenceEquals(client, active.BrokerClient) || ReferenceEquals(client, reclaimedClient)),
                    "The memory budget broker returned a client that is already registered.");

                configuration.ConfigurationVersion = this.policy.NextConfigurationVersion;

                this.policy.NextConfigurationVersion = checked(this.policy.NextConfigurationVersion + 1);

                active.State = MemoryPolicyConfigurationState.Retired;

                this.policy.Retired = active;
                this.policy.Active = configuration;
                isPublished = true;

                lock (this.allocationGate)
                {
                    this.policy.Epoch = checked(this.policy.Epoch + 1);
                }

                retiredClient = TryClaimRetiredConfiguration(active);
            }
        }
        finally
        {
            if (!isPublished)
            {
                client?.Dispose();
            }

            reclaimedClient?.Dispose();
            retiredClient?.Dispose();
        }
    }

    private IGraphicsMemoryBudgetClient? TryClaimRetiredConfiguration(MemoryPolicyConfiguration? configuration)
    {
        if (configuration is null ||
            !ReferenceEquals(this.policy.Retired, configuration) ||
            configuration.State is not MemoryPolicyConfigurationState.Retired ||
            configuration.LeaseCount != 0)
        {
            return null;
        }

        IGraphicsMemoryBudgetClient? client = configuration.BrokerClient;

        configuration.State = MemoryPolicyConfigurationState.Disposed;
        configuration.BrokerClient = null;

        this.policy.Retired = null;

        return client;
    }

    private void MergeBudgetObservation(MemoryPlacement placement, in VideoMemoryBudgetSnapshot budget)
    {
        ref SegmentMemoryAccounting accounting = ref GetSegment(placement);

        if (accounting.DxgiInitialized && IsSameObservation(in accounting.LastDxgiObservation, in budget))
        {
            return;
        }

        accounting.DxgiInitialized = true;
        accounting.LastDxgiObservation = budget;

        this.policy.Epoch = checked(this.policy.Epoch + 1);
    }

    private SegmentPolicySnapshot MergeSegmentObservation(
        MemoryPolicyConfiguration configuration,
        MemoryPlacement placement,
        in SegmentObservationInput input)
    {
        SegmentPolicySnapshot snapshot = new()
        {
            TopologyActive = input.TopologyActive,
            DxgiStatus = input.DxgiStatus,
            Dxgi = input.Dxgi,
            BrokerConfigured = input.BrokerConfigured,
            GrantStatus = BrokerGrantStatus.NotConfigured,
            ExplicitHardLimitBytes = configuration.GetExplicitHardLimitBytes(placement)
        };

        if (input.TopologyActive && input.DxgiStatus is MemoryBudgetStatus.Valid)
        {
            MergeBudgetObservation(placement, in input.Dxgi);
        }

        if (!input.BrokerConfigured)
        {
            return snapshot;
        }

        ref BrokerGrantObservation observation = ref configuration.GetGrantObservation(placement);

        if (!input.HasGrant)
        {
            snapshot.GrantStatus = BrokerGrantStatus.Unknown;
            snapshot.Grant = observation.Grant;

            return snapshot;
        }

        if (!observation.Initialized)
        {
            observation.Initialized = true;
            observation.Grant = input.Grant;

            this.policy.Epoch = checked(this.policy.Epoch + 1);
        }
        else if (!MemoryAdmission.IsGrantObservationValid(in observation.Grant, in input.Grant))
        {
            snapshot.GrantStatus = BrokerGrantStatus.Unknown;
            snapshot.Grant = observation.Grant;

            return snapshot;
        }
        else if (input.Grant.Version > observation.Grant.Version)
        {
            observation.Grant = input.Grant;

            this.policy.Epoch = checked(this.policy.Epoch + 1);
        }

        snapshot.GrantStatus = BrokerGrantStatus.Valid;
        snapshot.Grant = observation.Grant;

        return snapshot;
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
