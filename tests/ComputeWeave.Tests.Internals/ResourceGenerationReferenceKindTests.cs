using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceGenerationReferenceKindTests
{
    private enum ReferenceKind
    {
        Recording,
        PendingSubmission,
        External,
        Cpu,
        Native,
        PersistentLease
    }

    private static readonly ReferenceKind[] DeferringKinds =
    [
        ReferenceKind.Recording,
        ReferenceKind.PendingSubmission,
        ReferenceKind.External,
        ReferenceKind.Cpu,
        ReferenceKind.Native
    ];

    private static readonly ReferenceKind[] IdleExcludingKinds =
    [
        ReferenceKind.Recording,
        ReferenceKind.PendingSubmission,
        ReferenceKind.External,
        ReferenceKind.Cpu,
        ReferenceKind.Native,
        ReferenceKind.PersistentLease
    ];

    private static ResourceGenerationRecord ActiveRecord()
    {
        return new ResourceGenerationRecord
        {
            Id = new ResourceGenerationId(1),
            StateFlags = ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.ExternalObjectsReleasedBit,
            Lifecycle = ResourceGenerationState.Active,
            OwnerReferenceCount = 1
        };
    }

    private static bool TryAcquire(ref ResourceGenerationRecord record, ReferenceKind kind)
    {
        switch (kind)
        {
            case ReferenceKind.Recording:
                return record.TryAcquireRecordingReference();
            case ReferenceKind.PendingSubmission:
                if (!record.TryAcquireRecordingReference())
                {
                    return false;
                }

                record.ConvertRecordingToPendingSubmission();

                return true;
            case ReferenceKind.External:
                return record.TryAcquireExternalReference();
            case ReferenceKind.Cpu:
                return record.TryAcquireCpuReference();
            case ReferenceKind.Native:
                return record.TryAcquireNativeReference();
            default:
                return record.TryAcquirePersistentLease();
        }
    }

    private static void Release(ref ResourceGenerationRecord record, ReferenceKind kind)
    {
        switch (kind)
        {
            case ReferenceKind.Recording:
                record.ReleaseRecordingReference();
                break;
            case ReferenceKind.PendingSubmission:
                record.ReleasePendingSubmissionReference();
                break;
            case ReferenceKind.External:
                record.ReleaseExternalReference();
                break;
            case ReferenceKind.Cpu:
                record.ReleaseCpuReference();
                break;
            case ReferenceKind.Native:
                record.ReleaseNativeReference();
                break;
            default:
                record.ReleasePersistentLease();
                break;
        }
    }

    [TestMethod]
    public void DefersPromotionForEveryReferenceKindThatCounts()
    {
        foreach (ReferenceKind kind in DeferringKinds)
        {
            ResourceGenerationRecord record = ActiveRecord();

            Assert.IsTrue(TryAcquire(ref record, kind), kind.ToString());

            record.ReleaseOwnerReference();

            Assert.IsTrue(record.TryRequestRetire(), kind.ToString());
            Assert.IsFalse(record.TryPromoteRetiredReady(isRetirementFenceCompleted: true), kind.ToString());
            Assert.AreEqual(ResourceGenerationState.RetiredPending, record.ReadLifecycle(), kind.ToString());

            Release(ref record, kind);

            Assert.IsTrue(record.TryPromoteRetiredReady(isRetirementFenceCompleted: true), kind.ToString());
            Assert.AreEqual(ResourceGenerationState.RetiredReady, record.ReadLifecycle(), kind.ToString());
        }
    }

    [TestMethod]
    public void ExcludesEveryReferenceKindThatCountsFromIdle()
    {
        foreach (ReferenceKind kind in IdleExcludingKinds)
        {
            ResourceGenerationRecord record = ActiveRecord();

            Assert.IsTrue(record.IsIdle, kind.ToString());
            Assert.IsTrue(TryAcquire(ref record, kind), kind.ToString());
            Assert.IsFalse(record.IsIdle, kind.ToString());

            Release(ref record, kind);

            Assert.IsTrue(record.IsIdle, kind.ToString());
        }
    }

    [TestMethod]
    public void KeepsTheOwnerReferenceOutOfTheIdleCondition()
    {
        ResourceGenerationRecord record = ActiveRecord();

        Assert.AreEqual(1, record.OwnerReferenceCount);
        Assert.IsTrue(record.IsIdle);
        Assert.IsTrue(record.HasReferences);

        record.ReleaseOwnerReference();

        Assert.IsTrue(record.IsIdle);
        Assert.IsFalse(record.HasReferences);
    }

    [TestMethod]
    public void RejectsEveryReferenceKindOnceTheLifecycleLeavesActive()
    {
        foreach (ReferenceKind kind in DeferringKinds)
        {
            ResourceGenerationRecord record = ActiveRecord();

            Assert.IsTrue(record.TryRequestRetire(), kind.ToString());
            Assert.IsFalse(TryAcquire(ref record, kind), kind.ToString());
            Assert.IsFalse(record.HasQueueReferences, kind.ToString());
        }
    }

    [TestMethod]
    public void CountsEveryReferenceKindInTheReferencePredicate()
    {
        foreach (ReferenceKind kind in DeferringKinds)
        {
            ResourceGenerationRecord record = ActiveRecord();

            record.ReleaseOwnerReference();

            Assert.IsFalse(record.HasReferences, kind.ToString());
            Assert.IsTrue(TryAcquire(ref record, kind), kind.ToString());
            Assert.IsTrue(record.HasReferences, kind.ToString());

            Release(ref record, kind);

            Assert.IsFalse(record.HasReferences, kind.ToString());
        }
    }
}
