using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourceGenerationLifecycleRaceTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private ResourceGenerationRecord record = new()
        {
            Id = new ResourceGenerationId(1),
            Lifecycle = ResourceGenerationState.RetireRequested,
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

    private const int RoundCount = 128;

    private static int RunSimultaneously(Func<bool> body)
    {
        Thread[] threads = new Thread[ThreadCount];

        int successCount = 0;
        int readyCount = 0;
        bool isStarted = false;

        for (int i = 0; i < ThreadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                _ = Interlocked.Increment(ref readyCount);

                while (!Volatile.Read(ref isStarted))
                {
                    Thread.SpinWait(1);
                }

                if (body())
                {
                    _ = Interlocked.Increment(ref successCount);
                }
            });

            threads[i].Start();
        }

        while (Volatile.Read(ref readyCount) < ThreadCount)
        {
            Thread.SpinWait(1);
        }

        Volatile.Write(ref isStarted, true);

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        return successCount;
    }

    [TestMethod]
    public void UsesFourByteLifecycleStorageWithStableValues()
    {
        Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(ResourceGenerationState)));
        Assert.AreEqual(4, Unsafe.SizeOf<ResourceGenerationState>());
        Assert.AreEqual(0, (int)ResourceGenerationState.Constructing);
        Assert.AreEqual(1, (int)ResourceGenerationState.Active);
        Assert.AreEqual(2, (int)ResourceGenerationState.RetireRequested);
        Assert.AreEqual(3, (int)ResourceGenerationState.RetiredPending);
        Assert.AreEqual(4, (int)ResourceGenerationState.RetiredReady);
        Assert.AreEqual(5, (int)ResourceGenerationState.Releasing);
        Assert.AreEqual(6, (int)ResourceGenerationState.Released);
        Assert.AreEqual(7, (int)ResourceGenerationState.Faulted);
        Assert.AreEqual(8, (int)ResourceGenerationState.TerminalRetained);
    }

    [TestMethod]
    public void PromotesRetiredReadyExactlyOnce()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            int successCount = RunSimultaneously(() => owner.GetResourceRecord(0).TryPromoteRetiredReady(isRetirementFenceCompleted: true));

            Assert.AreEqual(1, successCount);
            Assert.AreEqual(ResourceGenerationState.RetiredReady, owner.GetResourceRecord(0).Lifecycle);
        }
    }

    [TestMethod]
    public void RequestsRetirementExactlyOnce()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.Active;
            owner.GetResourceRecord(0).OwnerReferenceCount = 1;

            int successCount = RunSimultaneously(() =>
            {
                if (!owner.GetResourceRecord(0).TryRequestRetire())
                {
                    return false;
                }

                owner.GetResourceRecord(0).ReleaseOwnerReference();

                return true;
            });

            Assert.AreEqual(1, successCount);
            Assert.AreEqual(0, owner.GetResourceRecord(0).OwnerReferenceCount);
        }
    }

    [TestMethod]
    public void BeginsReleaseExactlyOnce()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.RetiredReady;

            int successCount = RunSimultaneously(() => owner.GetResourceRecord(0).TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));

            Assert.AreEqual(1, successCount);
            Assert.AreEqual(ResourceGenerationState.Releasing, owner.GetResourceRecord(0).Lifecycle);
        }
    }

    [TestMethod]
    public void CompletesReleaseOnlyOnce()
    {
        GenerationOwner owner = new();

        owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.RetiredReady;

        Assert.IsTrue(owner.GetResourceRecord(0).TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(owner.GetResourceRecord(0).TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsFalse(owner.GetResourceRecord(0).TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
    }

    [TestMethod]
    public void KeepsLifecycleLegalWhenPromotionRacesTerminalTransition()
    {
        for (int attempt = 0; attempt < RoundCount; attempt++)
        {
            GenerationOwner owner = new();

            int promotionCount = 0;
            int terminalCount = 0;

            Parallel.Invoke(
                () =>
                {
                    if (owner.GetResourceRecord(0).TryPromoteRetiredReady(isRetirementFenceCompleted: true))
                    {
                        _ = Interlocked.Increment(ref promotionCount);
                    }
                },
                () =>
                {
                    if (owner.GetResourceRecord(0).TryMarkTerminalRetained())
                    {
                        _ = Interlocked.Increment(ref terminalCount);
                    }
                });

            ResourceGenerationState lifecycle = owner.GetResourceRecord(0).Lifecycle;

            Assert.IsTrue(
                lifecycle is ResourceGenerationState.RetiredReady or ResourceGenerationState.TerminalRetained,
                lifecycle.ToString());

            Assert.IsTrue(promotionCount <= 1);
            Assert.AreEqual(1, terminalCount);
        }
    }

    [TestMethod]
    public void RejectsReleaseWithMismatchedAuthority()
    {
        GenerationOwner owner = new();

        owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.TerminalRetained;

        Assert.IsFalse(owner.GetResourceRecord(0).TryBeginRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(owner.GetResourceRecord(0).TryBeginRelease(ResourceReleaseAuthority.DeviceTeardown));
        Assert.IsFalse(owner.GetResourceRecord(0).TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion));
        Assert.IsTrue(owner.GetResourceRecord(0).TryCompleteRelease(ResourceReleaseAuthority.DeviceTeardown));
        Assert.IsFalse(owner.GetResourceRecord(0).TryMarkTerminalRetained());
    }
}
