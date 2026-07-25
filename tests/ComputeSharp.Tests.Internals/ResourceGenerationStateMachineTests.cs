using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceGenerationStateMachineTests
{
    private static ResourceGenerationRecord ActiveRecord()
    {
        return new ResourceGenerationRecord
        {
            Id = new ResourceGenerationId(1),
            Lifecycle = ResourceGenerationState.Active,
            OwnerReferenceCount = 1,
            ExternalObjectsReleased = 1
        };
    }

    [TestMethod]
    public void AcquiresRecordingReferenceOnlyWhenActive()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireRecordingReference());
        Assert.AreEqual(1, record.RecordingReferenceCount);

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryAcquireRecordingReference());
        Assert.AreEqual(1, record.RecordingReferenceCount);
    }

    [TestMethod]
    public void RejectsDecrementBelowZero()
    {
        ResourceGenerationRecord record = ActiveRecord();

        _ = Assert.ThrowsException<InvalidOperationException>(() => record.ReleaseRecordingReference());
    }

    [TestMethod]
    public void PromotesToRetiredPendingWhileReferencesRemain()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryPromoteRetiredReady(true));
        Assert.AreEqual(ResourceGenerationState.RetiredPending, record.Lifecycle);
    }

    [TestMethod]
    public void PromotesToRetiredReadyWhenAllConditionsHold()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryRequestRetire());

        record.ReleaseOwnerReference();

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
        Assert.AreEqual(ResourceGenerationState.RetiredReady, record.Lifecycle);
    }

    [TestMethod]
    public void BlocksPromotionUntilRetirementFenceCompletes()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryRequestRetire());

        record.ReleaseOwnerReference();

        Assert.IsFalse(record.TryPromoteRetiredReady(false));
        Assert.AreEqual(ResourceGenerationState.RetiredPending, record.Lifecycle);
        Assert.IsTrue(record.TryPromoteRetiredReady(true));
    }

    [TestMethod]
    public void BlocksPromotionUntilExternalObjectsReleased()
    {
        ResourceGenerationRecord record = ActiveRecord();

        record.ExternalObjectsReleased = 0;

        Assert.IsTrue(record.TryRequestRetire());

        record.ReleaseOwnerReference();

        Assert.IsFalse(record.TryPromoteRetiredReady(true));

        record.ExternalObjectsReleased = 1;

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
    }

    [TestMethod]
    public void OrdersPendingIncrementBeforeRecordingDecrement()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireRecordingReference());

        record.AddPendingSubmissionReference();
        record.ReleaseRecordingReference();
        record.ReleaseOwnerReference();

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryPromoteRetiredReady(true));

        record.ReleasePendingSubmissionReference();

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
    }

    [TestMethod]
    public void EnforcesReleaseAuthority()
    {
        ResourceGenerationState[] lifecycles =
        [
            ResourceGenerationState.Constructing,
            ResourceGenerationState.Active,
            ResourceGenerationState.RetireRequested,
            ResourceGenerationState.RetiredPending,
            ResourceGenerationState.RetiredReady,
            ResourceGenerationState.Releasing,
            ResourceGenerationState.Released,
            ResourceGenerationState.Faulted,
            ResourceGenerationState.TerminalRetained
        ];

        ResourceReleaseAuthority[] authorities =
        [
            ResourceReleaseAuthority.NormalCompletion,
            ResourceReleaseAuthority.DomainTeardown,
            ResourceReleaseAuthority.DeviceTeardown
        ];

        foreach (ResourceGenerationState lifecycle in lifecycles)
        {
            foreach (ResourceReleaseAuthority authority in authorities)
            {
                ResourceGenerationRecord record = ActiveRecord();

                record.Lifecycle = lifecycle;

                bool expected =
                    (lifecycle is ResourceGenerationState.RetiredReady && authority is ResourceReleaseAuthority.NormalCompletion) ||
                    (lifecycle is ResourceGenerationState.Faulted && authority is ResourceReleaseAuthority.DomainTeardown) ||
                    (lifecycle is ResourceGenerationState.TerminalRetained && authority is ResourceReleaseAuthority.DeviceTeardown);

                Assert.AreEqual(expected, record.TryBeginRelease(authority));
            }
        }
    }

    [TestMethod]
    public void CompletesReleaseOnlyFromReleasing()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsFalse(record.TryCompleteRelease());

        record.Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(record.TryCompleteRelease());
        Assert.AreEqual(ResourceGenerationState.Released, record.Lifecycle);
        Assert.IsFalse(record.TryCompleteRelease());
    }

    [TestMethod]
    public void MarksTerminalRetainedForAnyNonReleasedState()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryMarkTerminalRetained());
        Assert.AreEqual(ResourceGenerationState.TerminalRetained, record.Lifecycle);

        record.Lifecycle = ResourceGenerationState.Released;

        Assert.IsFalse(record.TryMarkTerminalRetained());
    }
}
