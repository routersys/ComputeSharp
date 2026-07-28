using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ExternalOwnershipStateMachineTests
{
    private static ResourceGenerationRecord Record(ExternalOwnershipState ownership)
    {
        return new ResourceGenerationRecord
        {
            Id = new ResourceGenerationId(1),
            Lifecycle = ResourceGenerationState.Active,
            Ownership = ownership,
            OwnerReferenceCount = 1
        };
    }

    [TestMethod]
    public void RunsTheRoundTripOfAnExternallyOwnedGeneration()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.ExternalAvailable);

        Assert.IsTrue(record.TryMarkAcquireSignalEnqueued());
        Assert.AreEqual(ExternalOwnershipState.AcquireSignalEnqueued, record.ReadOwnership());

        Assert.IsTrue(record.TryMarkComputeExecutionIssued());
        Assert.AreEqual(ExternalOwnershipState.ComputeExecutionIssued, record.ReadOwnership());

        Assert.IsTrue(record.TryMarkReleaseSignalEnqueued());
        Assert.AreEqual(ExternalOwnershipState.ReleaseSignalEnqueued, record.ReadOwnership());

        Assert.IsTrue(record.TryMarkExternalAvailable());
        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, record.ReadOwnership());
    }

    [TestMethod]
    public void ReleasesAComputeOwnedGenerationWithoutAcquiringIt()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.ComputeAvailable);

        Assert.IsFalse(record.TryMarkAcquireSignalEnqueued());
        Assert.AreEqual(ExternalOwnershipState.ComputeAvailable, record.ReadOwnership());

        Assert.IsTrue(record.TryMarkComputeExecutionIssued());
        Assert.IsTrue(record.TryMarkReleaseSignalEnqueued());
        Assert.IsTrue(record.TryMarkExternalAvailable());
    }

    [TestMethod]
    public void RejectsEveryTransitionThatSkipsAStep()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.ExternalAvailable);

        Assert.IsFalse(record.TryMarkComputeExecutionIssued());
        Assert.IsFalse(record.TryMarkReleaseSignalEnqueued());
        Assert.IsFalse(record.TryMarkExternalAvailable());
        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, record.ReadOwnership());

        Assert.IsTrue(record.TryMarkAcquireSignalEnqueued());

        Assert.IsFalse(record.TryMarkAcquireSignalEnqueued());
        Assert.IsFalse(record.TryMarkReleaseSignalEnqueued());
        Assert.IsFalse(record.TryMarkExternalAvailable());
        Assert.AreEqual(ExternalOwnershipState.AcquireSignalEnqueued, record.ReadOwnership());
    }

    [TestMethod]
    public void FaultsFromAnyStateExactlyOnce()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.ComputeExecutionIssued);

        Assert.IsTrue(record.TryMarkOwnershipFaulted());
        Assert.AreEqual(ExternalOwnershipState.Faulted, record.ReadOwnership());

        Assert.IsFalse(record.TryMarkOwnershipFaulted());
    }

    [TestMethod]
    public void RejectsEveryTransitionOutOfTheFaultedState()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.Faulted);

        Assert.IsFalse(record.TryMarkAcquireSignalEnqueued());
        Assert.IsFalse(record.TryMarkComputeExecutionIssued());
        Assert.IsFalse(record.TryMarkReleaseSignalEnqueued());
        Assert.IsFalse(record.TryMarkExternalAvailable());
        Assert.AreEqual(ExternalOwnershipState.Faulted, record.ReadOwnership());
    }

    [TestMethod]
    public void KeepsTheAdjacentRecordFieldsIntact()
    {
        ResourceGenerationRecord record = Record(ExternalOwnershipState.ExternalAvailable);

        record.D3D12State = TrackedResourceState.UnorderedAccess;

        Assert.IsTrue(record.TryMarkAcquireSignalEnqueued());

        Assert.AreEqual(TrackedResourceState.UnorderedAccess, record.D3D12State);
        Assert.AreEqual(ResourceGenerationState.Active, record.ReadLifecycle());
        Assert.AreEqual(1, record.OwnerReferenceCount);
    }
}
