using ComputeSharp.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class MemoryAdmissionTests
{
    private static SegmentPolicySnapshot Segment(
        ulong budgetBytes = 1024,
        ulong currentUsageBytes = 0,
        bool brokerConfigured = false,
        BrokerGrantStatus grantStatus = BrokerGrantStatus.NotConfigured,
        bool hasLimit = false,
        ulong limitBytes = 0,
        ulong? explicitHardLimitBytes = null,
        bool topologyActive = true,
        MemoryBudgetStatus dxgiStatus = MemoryBudgetStatus.Valid)
    {
        return new SegmentPolicySnapshot
        {
            TopologyActive = topologyActive,
            DxgiStatus = dxgiStatus,
            Dxgi = new VideoMemoryBudgetSnapshot { BudgetBytes = budgetBytes, CurrentUsageBytes = currentUsageBytes },
            BrokerConfigured = brokerConfigured,
            GrantStatus = grantStatus,
            Grant = new GraphicsMemoryGrant { HasLimit = hasLimit, LimitBytes = limitBytes, Version = 1 },
            ExplicitHardLimitBytes = explicitHardLimitBytes
        };
    }

    private static SegmentMemoryAccounting Accounting(ulong ownedBytes = 0, ulong reservationBytes = 0)
    {
        return new SegmentMemoryAccounting { OwnedBytes = ownedBytes, ReservationBytes = reservationBytes };
    }

    [TestMethod]
    public void AdmitsWithinTheBudget()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            MemoryAdmission.Evaluate(Segment(budgetBytes: 1024, currentUsageBytes: 512), Accounting(), 512));
    }

    [TestMethod]
    public void RejectsBeyondTheBudget()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetExceeded,
            MemoryAdmission.Evaluate(Segment(budgetBytes: 1024, currentUsageBytes: 512), Accounting(), 513));
    }

    [TestMethod]
    public void CountsPendingReservationsInBothProjections()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetExceeded,
            MemoryAdmission.Evaluate(Segment(budgetBytes: 1024, currentUsageBytes: 512), Accounting(reservationBytes: 256), 257));

        Assert.AreEqual(
            MemoryAdmissionStatus.GrantExceeded,
            MemoryAdmission.Evaluate(
                Segment(brokerConfigured: true, grantStatus: BrokerGrantStatus.Valid, hasLimit: true, limitBytes: 512),
                Accounting(ownedBytes: 128, reservationBytes: 128),
                257));
    }

    [TestMethod]
    public void RejectsInactiveSegmentsBeforeAnyQuery()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.SegmentInactive,
            MemoryAdmission.Evaluate(Segment(topologyActive: false, dxgiStatus: MemoryBudgetStatus.Unsupported), Accounting(), 1));
    }

    [TestMethod]
    public void RejectsEveryNonValidBudgetStatus()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetUnavailable,
            MemoryAdmission.Evaluate(Segment(dxgiStatus: MemoryBudgetStatus.Unknown), Accounting(), 1));

        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetUnavailable,
            MemoryAdmission.Evaluate(Segment(dxgiStatus: MemoryBudgetStatus.DeviceLost), Accounting(), 1));

        Assert.AreEqual(
            MemoryAdmissionStatus.BudgetUnavailable,
            MemoryAdmission.Evaluate(Segment(dxgiStatus: MemoryBudgetStatus.Unsupported), Accounting(), 1));
    }

    [TestMethod]
    public void IgnoresTheGrantWhenNoBrokerIsConfigured()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            MemoryAdmission.Evaluate(
                Segment(brokerConfigured: false, grantStatus: BrokerGrantStatus.Unknown, hasLimit: true, limitBytes: 1),
                Accounting(),
                512));
    }

    [TestMethod]
    public void FailsClosedWhenAConfiguredGrantIsUnavailable()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.GrantUnavailable,
            MemoryAdmission.Evaluate(
                Segment(brokerConfigured: true, grantStatus: BrokerGrantStatus.Unknown),
                Accounting(),
                1));
    }

    [TestMethod]
    public void IgnoresTheLimitBytesOfAGrantWithoutALimit()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            MemoryAdmission.Evaluate(
                Segment(brokerConfigured: true, grantStatus: BrokerGrantStatus.Valid, hasLimit: false, limitBytes: 1),
                Accounting(ownedBytes: 512),
                512));
    }

    [TestMethod]
    public void AppliesTheExplicitHardLimitToOwnedBytes()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.Admitted,
            MemoryAdmission.Evaluate(Segment(explicitHardLimitBytes: 512), Accounting(ownedBytes: 256), 256));

        Assert.AreEqual(
            MemoryAdmissionStatus.ExplicitLimitExceeded,
            MemoryAdmission.Evaluate(Segment(explicitHardLimitBytes: 512), Accounting(ownedBytes: 256), 257));
    }

    [TestMethod]
    public void RejectsArithmeticOverflowWithoutWrapping()
    {
        Assert.AreEqual(
            MemoryAdmissionStatus.ArithmeticOverflow,
            MemoryAdmission.Evaluate(Segment(budgetBytes: ulong.MaxValue, currentUsageBytes: ulong.MaxValue), Accounting(), 1));

        Assert.AreEqual(
            MemoryAdmissionStatus.ArithmeticOverflow,
            MemoryAdmission.Evaluate(Segment(budgetBytes: ulong.MaxValue), Accounting(ownedBytes: ulong.MaxValue), 1));
    }

    [TestMethod]
    public void DetectsBrokerContractViolations()
    {
        GraphicsMemoryGrant previous = new() { HasLimit = true, LimitBytes = 256, Version = 4 };

        Assert.IsTrue(MemoryAdmission.IsGrantObservationValid(previous, previous));
        Assert.IsTrue(MemoryAdmission.IsGrantObservationValid(previous, new GraphicsMemoryGrant { HasLimit = false, Version = 5 }));

        Assert.IsFalse(MemoryAdmission.IsGrantObservationValid(previous, new GraphicsMemoryGrant { HasLimit = true, LimitBytes = 256, Version = 3 }));
        Assert.IsFalse(MemoryAdmission.IsGrantObservationValid(previous, new GraphicsMemoryGrant { HasLimit = true, LimitBytes = 512, Version = 4 }));
        Assert.IsFalse(MemoryAdmission.IsGrantObservationValid(previous, new GraphicsMemoryGrant { HasLimit = false, Version = 4 }));
    }
}
