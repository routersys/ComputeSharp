using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceGenerationStateMachineTests
{
    private static ResourceGenerationRecord ActiveRecord()
    {
        ResourceGenerationRecord record = new()
        {
            Id = new ResourceGenerationId(1),
            StateFlags = ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.ExternalObjectsReleasedBit,
            Lifecycle = ResourceGenerationState.Active,
            OwnerReferenceCount = 1
        };

        return record;
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

        record.StateFlags &= ~ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.ExternalObjectsReleasedBit;

        Assert.IsTrue(record.TryRequestRetire());

        record.ReleaseOwnerReference();

        Assert.IsFalse(record.TryPromoteRetiredReady(true));

        record.StateFlags |= ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.ExternalObjectsReleasedBit;

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
    }

    [TestMethod]
    public void OrdersPendingIncrementBeforeRecordingDecrement()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireRecordingReference());

        record.ConvertRecordingToPendingSubmission();

        Assert.AreEqual(0, record.RecordingReferenceCount);
        Assert.AreEqual(1, record.PendingSubmissionReferenceCount);

        record.ReleaseOwnerReference();

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryPromoteRetiredReady(true));

        record.ReleasePendingSubmissionReference();

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
    }

    [TestMethod]
    public void AcquiresNativeReferenceOnlyWhenActive()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireNativeReference());
        Assert.AreEqual(1, record.NativeReferenceCount);

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryAcquireNativeReference());
        Assert.AreEqual(1, record.NativeReferenceCount);
    }

    [TestMethod]
    public void BlocksPromotionWhileNativeReferenceRemains()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireNativeReference());

        record.ReleaseOwnerReference();

        Assert.IsTrue(record.TryRequestRetire());
        Assert.IsFalse(record.TryPromoteRetiredReady(true));
        Assert.AreEqual(ResourceGenerationState.RetiredPending, record.Lifecycle);

        record.ReleaseNativeReference();

        Assert.IsTrue(record.TryPromoteRetiredReady(true));
        Assert.AreEqual(ResourceGenerationState.RetiredReady, record.Lifecycle);
    }

    [TestMethod]
    public void ExcludesNativeReferencedGenerationsFromIdle()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.IsIdle);
        Assert.IsTrue(record.TryAcquireNativeReference());
        Assert.IsFalse(record.IsIdle);

        record.ReleaseNativeReference();

        Assert.IsTrue(record.IsIdle);
    }

    [TestMethod]
    public void RejectsNativeReferenceDecrementBelowZero()
    {
        ResourceGenerationRecord record = ActiveRecord();

        _ = Assert.ThrowsException<InvalidOperationException>(() => record.ReleaseNativeReference());
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

        Assert.IsFalse(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));

        record.Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.AreEqual(ResourceGenerationState.Released, record.Lifecycle);
        Assert.IsFalse(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
    }

    [TestMethod]
    public void RejectsReleaseCompletionWithMismatchedAuthority()
    {
        ResourceGenerationRecord record = ActiveRecord();

        record.Lifecycle = ResourceGenerationState.Faulted;

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.DomainTeardown));
        Assert.IsFalse(record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsFalse(record.TryCompleteRelease(ResourceReleaseAuthority.DeviceTeardown));
        Assert.AreEqual(ResourceGenerationState.Releasing, record.Lifecycle);
        Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.DomainTeardown));
    }

    [TestMethod]
    public void MarksTerminalRetainedForAnyNonReleasingState()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryMarkTerminalRetained());
        Assert.AreEqual(ResourceGenerationState.TerminalRetained, record.Lifecycle);

        record.Lifecycle = ResourceGenerationState.Released;

        Assert.IsFalse(record.TryMarkTerminalRetained());
    }

    [TestMethod]
    public void RejectsTerminalRetainedRegressionFromReleasing()
    {
        ResourceGenerationRecord record = ActiveRecord();

        record.Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsFalse(record.TryMarkTerminalRetained());
        Assert.AreEqual(ResourceGenerationState.Releasing, record.Lifecycle);
    }

    [TestMethod]
    public void PreservesTotalReferenceCountOnConversion()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.IsTrue(record.TryAcquireRecordingReference());
        Assert.IsTrue(record.TryAcquireRecordingReference());

        int before = record.RecordingReferenceCount + record.PendingSubmissionReferenceCount;

        record.ConvertRecordingToPendingSubmission();

        Assert.AreEqual(before, record.RecordingReferenceCount + record.PendingSubmissionReferenceCount);
        Assert.AreEqual(1, record.RecordingReferenceCount);
        Assert.AreEqual(1, record.PendingSubmissionReferenceCount);
    }

    [TestMethod]
    public void RejectsConversionWithoutRecordingReference()
    {
        ResourceGenerationRecord record = ActiveRecord();

        _ = Assert.ThrowsException<InvalidOperationException>(() => record.ConvertRecordingToPendingSubmission());
    }
}
