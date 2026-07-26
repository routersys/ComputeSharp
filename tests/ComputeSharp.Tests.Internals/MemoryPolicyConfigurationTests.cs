using System;
using System.Collections.Generic;
using ComputeSharp.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class MemoryPolicyConfigurationTests
{
    private sealed class BudgetClient : IGraphicsMemoryBudgetClient
    {
        private readonly Dictionary<GraphicsMemorySegment, GraphicsMemoryGrant> grants = [];

        public bool IsDisposed { get; private set; }

        public bool IsFailing { get; set; }

        public bool IsThrowing { get; set; }

        public int GrantQueryCount { get; private set; }

        public void SetGrant(GraphicsMemorySegment segment, bool hasLimit, ulong limitBytes, ulong version)
        {
            this.grants[segment] = new GraphicsMemoryGrant { HasLimit = hasLimit, LimitBytes = limitBytes, Version = version };
        }

        public bool TryGetGrant(GraphicsMemorySegment segment, out GraphicsMemoryGrant grant)
        {
            GrantQueryCount++;

            if (this.IsThrowing)
            {
                throw new NotSupportedException();
            }

            if (this.IsFailing || !this.grants.TryGetValue(segment, out grant))
            {
                grant = default;

                return false;
            }

            return true;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class BudgetBroker : IGraphicsMemoryBudgetBroker
    {
        private readonly List<BudgetClient> clients = [];

        public bool IsReturningTheSameClient { get; set; }

        public bool IsReturningNoClient { get; set; }

        public Action<BudgetClient>? Configure { get; set; }

        public IReadOnlyList<BudgetClient> Clients => this.clients;

        public GraphicsMemoryClientDescriptor LastDescriptor { get; private set; }

        public IGraphicsMemoryBudgetClient RegisterClient(in GraphicsMemoryClientDescriptor descriptor)
        {
            LastDescriptor = descriptor;

            if (this.IsReturningNoClient)
            {
                return null!;
            }

            if (this.IsReturningTheSameClient && this.clients.Count > 0)
            {
                return this.clients[^1];
            }

            BudgetClient client = new();

            client.SetGrant(GraphicsMemorySegment.Local, hasLimit: true, limitBytes: 4096, version: 1);
            client.SetGrant(GraphicsMemorySegment.NonLocal, hasLimit: true, limitBytes: 4096, version: 1);

            Configure?.Invoke(client);

            this.clients.Add(client);

            return client;
        }
    }

    private static SegmentObservationInput Observation(
        bool brokerConfigured = false,
        bool hasGrant = false,
        bool hasLimit = false,
        ulong limitBytes = 0,
        ulong version = 0,
        ulong currentUsageBytes = 0)
    {
        return new SegmentObservationInput
        {
            TopologyActive = true,
            DxgiStatus = MemoryBudgetStatus.Valid,
            Dxgi = new VideoMemoryBudgetSnapshot { BudgetBytes = 8192, CurrentUsageBytes = currentUsageBytes },
            BrokerConfigured = brokerConfigured,
            HasGrant = hasGrant,
            Grant = new GraphicsMemoryGrant { HasLimit = hasLimit, LimitBytes = limitBytes, Version = version }
        };
    }

    private static void SetPolicy(MemoryAllocationCoordinator coordinator, in GraphicsMemoryPolicy policy, bool isUma = false)
    {
        GraphicsMemoryClientDescriptor descriptor = new() { AdapterLuid = 42, NodeIndex = 0 };

        coordinator.SetPolicy(in policy, in descriptor, isUma);
    }

    [TestMethod]
    public void StartsWithoutABrokerAndWithoutHardLimits()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        Assert.IsNull(lease.Configuration.BrokerClient);
        Assert.IsNull(lease.Configuration.LocalOwnedHardLimitBytes);
        Assert.IsNull(lease.Configuration.NonLocalOwnedHardLimitBytes);
        Assert.AreEqual(1ul, lease.Configuration.ConfigurationVersion);
        Assert.AreEqual(MemoryPolicyConfigurationState.Active, lease.Configuration.State);
        Assert.IsFalse(coordinator.HasRetiredConfiguration);
    }

    [TestMethod]
    public void PublishesANewConfigurationVersionAndAdvancesTheEpoch()
    {
        MemoryAllocationCoordinator coordinator = new();

        ulong epoch = coordinator.Epoch;

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 1024 });

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        Assert.AreEqual(2ul, lease.Configuration.ConfigurationVersion);
        Assert.AreEqual(1024ul, lease.Configuration.LocalOwnedHardLimitBytes);
        Assert.IsTrue(coordinator.Epoch > epoch);
        Assert.IsFalse(coordinator.HasRetiredConfiguration);
    }

    [TestMethod]
    public void RegistersAndRetiresBrokerClientsExactlyOnce()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker broker = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        Assert.AreEqual(1, broker.Clients.Count);
        Assert.AreEqual(42L, broker.LastDescriptor.AdapterLuid);
        Assert.AreEqual(0u, broker.LastDescriptor.NodeIndex);
        Assert.IsFalse(broker.Clients[0].IsDisposed);

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        Assert.AreEqual(2, broker.Clients.Count);
        Assert.IsTrue(broker.Clients[0].IsDisposed);
        Assert.IsFalse(broker.Clients[1].IsDisposed);

        SetPolicy(coordinator, new GraphicsMemoryPolicy());

        Assert.IsTrue(broker.Clients[1].IsDisposed);
    }

    [TestMethod]
    public void KeepsTheRetiredConfigurationUntilItsLastLeaseIsReleased()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker broker = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SetPolicy(coordinator, new GraphicsMemoryPolicy());

        Assert.IsTrue(coordinator.HasRetiredConfiguration);
        Assert.IsFalse(broker.Clients[0].IsDisposed);

        lease.Dispose();

        Assert.IsFalse(coordinator.HasRetiredConfiguration);
        Assert.IsTrue(broker.Clients[0].IsDisposed);
    }

    [TestMethod]
    public void RejectsAPolicyUpdateWhileTheRetiredConfigurationIsLeased()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker broker = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SetPolicy(coordinator, new GraphicsMemoryPolicy());

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => SetPolicy(coordinator, new GraphicsMemoryPolicy()));

        Assert.IsTrue(coordinator.HasRetiredConfiguration);
    }

    [TestMethod]
    public void RollsBackAPolicyUpdateWhenTheBrokerFails()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker failingGrantBroker = new() { Configure = static client => client.IsFailing = true };

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = failingGrantBroker }));

        Assert.IsTrue(failingGrantBroker.Clients[0].IsDisposed);

        BudgetBroker throwingBroker = new() { Configure = static client => client.IsThrowing = true };

        _ = Assert.ThrowsExactly<NotSupportedException>(() => SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = throwingBroker }));

        Assert.IsTrue(throwingBroker.Clients[0].IsDisposed);

        BudgetBroker noClientBroker = new() { IsReturningNoClient = true };

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = noClientBroker }));

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        Assert.AreEqual(1ul, lease.Configuration.ConfigurationVersion);
        Assert.IsNull(lease.Configuration.BrokerClient);
        Assert.IsFalse(coordinator.HasRetiredConfiguration);
    }

    [TestMethod]
    public void RejectsABrokerThatReturnsAnAlreadyRegisteredClient()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker broker = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        broker.IsReturningTheSameClient = true;

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker }));

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        Assert.AreEqual(2ul, lease.Configuration.ConfigurationVersion);
        Assert.AreSame(broker.Clients[0], lease.Configuration.BrokerClient);
        Assert.IsFalse(broker.Clients[0].IsDisposed);
    }

    [TestMethod]
    public void OnlyQueriesTheGrantOfActiveSegments()
    {
        MemoryAllocationCoordinator unifiedCoordinator = new();
        BudgetBroker unifiedBroker = new();

        SetPolicy(unifiedCoordinator, new GraphicsMemoryPolicy { BudgetBroker = unifiedBroker }, isUma: true);

        Assert.AreEqual(1, unifiedBroker.Clients[0].GrantQueryCount);

        MemoryAllocationCoordinator discreteCoordinator = new();
        BudgetBroker discreteBroker = new();

        SetPolicy(discreteCoordinator, new GraphicsMemoryPolicy { BudgetBroker = discreteBroker }, isUma: false);

        Assert.AreEqual(2, discreteBroker.Clients[0].GrantQueryCount);
    }

    [TestMethod]
    public void AdmitsWithTheDxgiBudgetAloneWhenNoBrokerIsConfigured()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput input = Observation();
        MemoryAdmissionSnapshot snapshot = coordinator.Observe(lease.Configuration, in input, in input, default);

        Assert.AreEqual(BrokerGrantStatus.NotConfigured, snapshot.Local.GrantStatus);
        Assert.IsFalse(snapshot.Local.BrokerConfigured);

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, snapshot.Epoch, 8192, out _));
    }

    [TestMethod]
    public void FailsClosedWhenAConfiguredBrokerReportsNoGrant()
    {
        MemoryAllocationCoordinator coordinator = new();
        BudgetBroker broker = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = broker });

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput input = Observation(brokerConfigured: true, hasGrant: false);
        MemoryAdmissionSnapshot snapshot = coordinator.Observe(lease.Configuration, in input, in input, default);

        Assert.AreEqual(BrokerGrantStatus.Unknown, snapshot.Local.GrantStatus);

        Assert.AreEqual(
            MemoryAdmissionStatus.GrantUnavailable,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, snapshot.Epoch, 1, out _));
    }

    [TestMethod]
    public void AdvancesTheEpochOnlyForNewGrantObservations()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput first = Observation(brokerConfigured: true, hasGrant: true, hasLimit: true, limitBytes: 4096, version: 1);
        MemoryAdmissionSnapshot firstSnapshot = coordinator.Observe(lease.Configuration, in first, in first, default);

        Assert.AreEqual(BrokerGrantStatus.Valid, firstSnapshot.Local.GrantStatus);
        Assert.AreEqual(4096ul, firstSnapshot.Local.Grant.LimitBytes);

        MemoryAdmissionSnapshot secondSnapshot = coordinator.Observe(lease.Configuration, in first, in first, default);

        Assert.AreEqual(firstSnapshot.Epoch, secondSnapshot.Epoch);

        SegmentObservationInput refreshed = Observation(brokerConfigured: true, hasGrant: true, hasLimit: true, limitBytes: 2048, version: 2);
        MemoryAdmissionSnapshot refreshedSnapshot = coordinator.Observe(lease.Configuration, in refreshed, in refreshed, default);

        Assert.IsTrue(refreshedSnapshot.Epoch > secondSnapshot.Epoch);
        Assert.AreEqual(2048ul, refreshedSnapshot.Local.Grant.LimitBytes);
    }

    [TestMethod]
    public void FailsClosedWhenTheGrantVersionIsNotMonotonic()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput initial = Observation(brokerConfigured: true, hasGrant: true, hasLimit: true, limitBytes: 4096, version: 4);

        _ = coordinator.Observe(lease.Configuration, in initial, in initial, default);

        SegmentObservationInput regressed = Observation(brokerConfigured: true, hasGrant: true, hasLimit: true, limitBytes: 4096, version: 3);
        MemoryAdmissionSnapshot regressedSnapshot = coordinator.Observe(lease.Configuration, in regressed, in regressed, default);

        Assert.AreEqual(BrokerGrantStatus.Unknown, regressedSnapshot.Local.GrantStatus);

        SegmentObservationInput mutated = Observation(brokerConfigured: true, hasGrant: true, hasLimit: true, limitBytes: 2048, version: 4);
        MemoryAdmissionSnapshot mutatedSnapshot = coordinator.Observe(lease.Configuration, in mutated, in mutated, default);

        Assert.AreEqual(BrokerGrantStatus.Unknown, mutatedSnapshot.Local.GrantStatus);
        Assert.AreEqual(4096ul, mutatedSnapshot.Local.Grant.LimitBytes);
    }

    [TestMethod]
    public void IgnoresTheGrantLimitWhenTheGrantHasNoLimit()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput input = Observation(brokerConfigured: true, hasGrant: true, hasLimit: false, limitBytes: 1, version: 1);
        MemoryAdmissionSnapshot snapshot = coordinator.Observe(lease.Configuration, in input, in input, default);

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, snapshot.Epoch, 8192, out _));
    }

    [TestMethod]
    public void EnforcesTheExplicitHardLimitOfEverySegment()
    {
        MemoryAllocationCoordinator coordinator = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 0, NonLocalOwnedHardLimitBytes = 1024 });

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput input = Observation();
        MemoryAdmissionSnapshot snapshot = coordinator.Observe(lease.Configuration, in input, in input, default);

        Assert.AreEqual(
            MemoryAdmissionStatus.ExplicitLimitExceeded,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, snapshot.Epoch, 1, out _));

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.NonLocal, snapshot.NonLocal, snapshot.Epoch, 1024, out _));

        Assert.AreEqual(
            MemoryAdmissionStatus.ExplicitLimitExceeded,
            coordinator.TryReserve(MemoryPlacement.NonLocal, snapshot.NonLocal, coordinator.Epoch, 1, out _));
    }

    [TestMethod]
    public void RequestsATrimOnlyForStricterPolicies()
    {
        MemoryAllocationCoordinator coordinator = new();

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 1024 });

        Assert.IsTrue(coordinator.TryClaimTrimRequest());
        Assert.IsFalse(coordinator.TryClaimTrimRequest());

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 2048 });

        Assert.IsFalse(coordinator.TryClaimTrimRequest());

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 512 });

        Assert.IsTrue(coordinator.TryClaimTrimRequest());

        SetPolicy(coordinator, new GraphicsMemoryPolicy { BudgetBroker = new BudgetBroker(), LocalOwnedHardLimitBytes = 512 });

        Assert.IsTrue(coordinator.TryClaimTrimRequest());
    }

    [TestMethod]
    public void KeepsAdmittingWithTheLeasedConfigurationAfterAPolicySwap()
    {
        MemoryAllocationCoordinator coordinator = new();

        using PolicyConfigurationLease lease = coordinator.AcquireConfigurationLease();

        SegmentObservationInput input = Observation();
        MemoryAdmissionSnapshot snapshot = coordinator.Observe(lease.Configuration, in input, in input, default);

        SetPolicy(coordinator, new GraphicsMemoryPolicy { LocalOwnedHardLimitBytes = 0 });

        Assert.AreEqual(
            MemoryAdmissionStatus.StaleSnapshot,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, snapshot.Epoch, 1, out _));

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, snapshot.Local, coordinator.Epoch, 1, out _));
    }
}
