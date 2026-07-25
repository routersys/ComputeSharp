using System.Threading.Tasks;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceGenerationReferenceRaceTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private ResourceGenerationRecord record = new()
        {
            Id = new ResourceGenerationId(1),
            Lifecycle = ResourceGenerationState.Active,
            ExternalObjectsReleased = 1
        };

        public ResourceGenerationSetId SetId => new(1);

        public int ResourceCount => 1;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.record;
        }
    }

    private const int ThreadCount = 8;

    private const int IterationCount = 2000;

    private static void RunConcurrently(System.Action<int> body)
    {
        _ = Parallel.For(0, ThreadCount, index =>
        {
            for (int i = 0; i < IterationCount; i++)
            {
                body(index);
            }
        });
    }

    [TestMethod]
    public void CountsEveryConcurrentRecordingReference()
    {
        GenerationOwner owner = new();

        RunConcurrently(_ => Assert.IsTrue(owner.GetResourceRecord(0).TryAcquireRecordingReference()));

        Assert.AreEqual(ThreadCount * IterationCount, owner.GetResourceRecord(0).RecordingReferenceCount);

        RunConcurrently(_ => owner.GetResourceRecord(0).ReleaseRecordingReference());

        Assert.AreEqual(0, owner.GetResourceRecord(0).RecordingReferenceCount);
        Assert.IsFalse(owner.GetResourceRecord(0).HasReferences);
    }

    [TestMethod]
    public void ConvertsEveryConcurrentRecordingReferenceExactlyOnce()
    {
        GenerationOwner owner = new();

        RunConcurrently(_ => Assert.IsTrue(owner.GetResourceRecord(0).TryAcquireRecordingReference()));
        RunConcurrently(_ => owner.GetResourceRecord(0).ConvertRecordingToPendingSubmission());

        Assert.AreEqual(0, owner.GetResourceRecord(0).RecordingReferenceCount);
        Assert.AreEqual(ThreadCount * IterationCount, owner.GetResourceRecord(0).PendingSubmissionReferenceCount);

        RunConcurrently(_ => owner.GetResourceRecord(0).ReleasePendingSubmissionReference());

        Assert.AreEqual(0, owner.GetResourceRecord(0).PendingSubmissionReferenceCount);
    }

    [TestMethod]
    public void CountsEveryConcurrentExternalAndCpuReference()
    {
        GenerationOwner owner = new();

        RunConcurrently(index =>
        {
            if ((index & 1) == 0)
            {
                Assert.IsTrue(owner.GetResourceRecord(0).TryAcquireExternalReference());
            }
            else
            {
                Assert.IsTrue(owner.GetResourceRecord(0).TryAcquireCpuReference());
            }
        });

        Assert.AreEqual(ThreadCount / 2 * IterationCount, owner.GetResourceRecord(0).ExternalReferenceCount);
        Assert.AreEqual(ThreadCount / 2 * IterationCount, owner.GetResourceRecord(0).CpuReferenceCount);
    }
}
