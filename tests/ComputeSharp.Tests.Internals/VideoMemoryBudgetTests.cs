using ComputeSharp.Memory;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public partial class VideoMemoryBudgetTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void QueriesTheLocalSegmentBudget(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        MemoryBudgetStatus status = graphicsDevice.TryQueryMemoryBudget(MemoryPlacement.Local, out VideoMemoryBudgetSnapshot budget);

        Assert.AreEqual(MemoryBudgetStatus.Valid, status);
        Assert.AreNotEqual(0ul, budget.BudgetBytes);
        Assert.IsTrue(budget.CurrentUsageBytes <= budget.BudgetBytes + budget.AvailableForReservationBytes);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsInactiveSegmentsAsUnsupported(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        MemoryBudgetStatus status = graphicsDevice.TryQueryMemoryBudget(MemoryPlacement.NonLocal, out VideoMemoryBudgetSnapshot budget);

        if (graphicsDevice.IsUma)
        {
            Assert.AreEqual(MemoryBudgetStatus.Unsupported, status);
            Assert.AreEqual(0ul, budget.BudgetBytes);
            Assert.AreEqual(0ul, budget.CurrentUsageBytes);
        }
        else
        {
            Assert.AreEqual(MemoryBudgetStatus.Valid, status);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void FeedsTheAdmissionCoordinatorWithLiveObservations(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        MemoryAllocationCoordinator coordinator = new();

        Assert.AreEqual(MemoryBudgetStatus.Valid, graphicsDevice.TryQueryMemoryBudget(MemoryPlacement.Local, out VideoMemoryBudgetSnapshot budget));

        ulong epoch = coordinator.ObserveBudget(MemoryPlacement.Local, budget);

        Assert.AreEqual(epoch, coordinator.ObserveBudget(MemoryPlacement.Local, budget));

        SegmentPolicySnapshot segment = new()
        {
            TopologyActive = true,
            DxgiStatus = MemoryBudgetStatus.Valid,
            Dxgi = budget,
            BrokerConfigured = false,
            GrantStatus = BrokerGrantStatus.NotConfigured
        };

        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            coordinator.TryReserve(MemoryPlacement.Local, segment, epoch, 65536, out MemoryReservationToken token));

        coordinator.AbortReservation(token);

        Assert.AreEqual(0ul, coordinator.GetAccounting(MemoryPlacement.Local).ReservationBytes);

        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetExceeded,
            coordinator.TryReserve(MemoryPlacement.Local, segment, epoch, budget.BudgetBytes + 1, out _));
    }
}
