using System;
using System.Threading;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class ResourceGenerationStateFlagRaceTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private ResourceGenerationRecord record = new()
        {
            Id = new ResourceGenerationId(1),
            StateFlags = ComputeWeave.Resources.Lifetime.ResourceGenerationRecord.ExternalObjectsReleasedBit,
            Lifecycle = ResourceGenerationState.Active,
            Ownership = ExternalOwnershipState.ComputeAvailable,
            D3D12State = TrackedResourceState.UnorderedAccess,
            Placement = MemoryPlacement.NonLocal,
            Recovery = ComputeResourceRecovery.Recompute,
            ReleaseAuthority = ResourceReleaseAuthority.DeviceTeardown,
            OwnerReferenceCount = 1
        };

        public ResourceGenerationSetId SetId => new(1);

        public int ResourceCount => 1;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.record;
        }

        public ID3D12Resource* GetResourceNativePointer(int resourceOrdinal)
        {
            return null;
        }
    }

    private const int ThreadCount = 8;

    private const int RoundCount = 128;

    // The most ManualResetEventSlim accepts; a smaller count stops catching the race.
    private const int ReleaseSpinCount = 2047;

    private static int RunSimultaneously(Func<int, bool> body)
    {
        Thread[] threads = new Thread[ThreadCount];

        int successCount = 0;

        // The waiters spin before they block: tight where processors allow, no starvation where they do not.
        using CountdownEvent ready = new(ThreadCount);
        using ManualResetEventSlim start = new(false, ReleaseSpinCount);

        for (int i = 0; i < ThreadCount; i++)
        {
            int index = i;

            threads[i] = new Thread(() =>
            {
                _ = ready.Signal();

                start.Wait();

                if (body(index))
                {
                    _ = Interlocked.Increment(ref successCount);
                }
            });

            threads[i].Start();
        }

        ready.Wait();
        start.Set();

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        return successCount;
    }

    [TestMethod]
    public void IssuesComputeExecutionExactlyOnceUnderContention()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            int successCount = RunSimultaneously(_ => owner.GetResourceRecord(0).TryMarkComputeExecutionIssued());

            Assert.AreEqual(1, successCount);
            Assert.AreEqual(ExternalOwnershipState.ComputeExecutionIssued, owner.GetResourceRecord(0).ReadOwnership());
        }
    }

    [TestMethod]
    public void FaultsOwnershipExactlyOnceUnderContention()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            int successCount = RunSimultaneously(_ => owner.GetResourceRecord(0).TryMarkOwnershipFaulted());

            Assert.AreEqual(1, successCount);
            Assert.AreEqual(ExternalOwnershipState.Faulted, owner.GetResourceRecord(0).ReadOwnership());
        }
    }

    [TestMethod]
    public void NeverLosesTheFaultWhenTheExecutionIssueRacesIt()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            int issuedCount = 0;
            int faultedCount = 0;

            _ = RunSimultaneously(index =>
            {
                if ((index & 1) == 0)
                {
                    if (owner.GetResourceRecord(0).TryMarkComputeExecutionIssued())
                    {
                        _ = Interlocked.Increment(ref issuedCount);
                    }
                }
                else if (owner.GetResourceRecord(0).TryMarkOwnershipFaulted())
                {
                    _ = Interlocked.Increment(ref faultedCount);
                }

                return false;
            });

            Assert.AreEqual(1, faultedCount);
            Assert.IsTrue(issuedCount <= 1);
            Assert.AreEqual(ExternalOwnershipState.Faulted, owner.GetResourceRecord(0).ReadOwnership());
        }
    }

    [TestMethod]
    public void KeepsEveryPackedFieldIntactWhenTheOwnershipRacesTheLifecycle()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            int faultedCount = 0;
            int retiredCount = 0;

            _ = RunSimultaneously(index =>
            {
                if ((index & 1) == 0)
                {
                    if (owner.GetResourceRecord(0).TryMarkOwnershipFaulted())
                    {
                        _ = Interlocked.Increment(ref faultedCount);
                    }
                }
                else if (owner.GetResourceRecord(0).TryRequestRetire())
                {
                    _ = Interlocked.Increment(ref retiredCount);
                }

                return false;
            });

            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(0);

            Assert.AreEqual(1, faultedCount);
            Assert.AreEqual(1, retiredCount);
            Assert.AreEqual(ExternalOwnershipState.Faulted, record.ReadOwnership());
            Assert.AreEqual(ResourceGenerationState.RetireRequested, record.ReadLifecycle());
            Assert.AreEqual(TrackedResourceState.UnorderedAccess, record.D3D12State);
            Assert.AreEqual(MemoryPlacement.NonLocal, record.Placement);
            Assert.AreEqual(ComputeResourceRecovery.Recompute, record.Recovery);
            Assert.AreEqual(ResourceReleaseAuthority.DeviceTeardown, record.ReleaseAuthority);
            Assert.AreEqual(1, record.OwnerReferenceCount);
            Assert.IsTrue(record.IsExternalObjectsReleased);
        }
    }

    [TestMethod]
    public void NeverCompletesAReleaseWithAnAuthorityThatDidNotBeginIt()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.TerminalRetained;
            owner.GetResourceRecord(0).ReleaseAuthority = ResourceReleaseAuthority.NormalCompletion;

            int begunCount = 0;
            int completedCount = 0;

            _ = RunSimultaneously(index =>
            {
                if (index == 0)
                {
                    if (owner.GetResourceRecord(0).TryBeginRelease(ResourceReleaseAuthority.DeviceTeardown))
                    {
                        _ = Interlocked.Increment(ref begunCount);
                    }
                }
                else if (owner.GetResourceRecord(0).TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion))
                {
                    _ = Interlocked.Increment(ref completedCount);
                }

                return false;
            });

            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(0);

            Assert.AreEqual(1, begunCount);
            Assert.AreEqual(0, completedCount);
            Assert.AreEqual(ResourceGenerationState.Releasing, record.ReadLifecycle());
            Assert.AreEqual(ResourceReleaseAuthority.DeviceTeardown, record.ReleaseAuthority);
            Assert.IsTrue(record.TryCompleteRelease(ResourceReleaseAuthority.DeviceTeardown));
            Assert.AreEqual(ResourceGenerationState.Released, record.ReadLifecycle());
        }
    }

    [TestMethod]
    public void KeepsEveryFenceQueueInItsOwnBitRange()
    {
        GenerationOwner owner = new();

        ref ResourceGenerationRecord record = ref owner.GetResourceRecord(0);

        record.LastComputeRead = new FencePoint(ComputeQueueKind.Compute, 11);
        record.LastCopyRead = new FencePoint(ComputeQueueKind.Copy, 22);
        record.LastWrite = new FencePoint(ComputeQueueKind.Compute, 33);
        record.RetirementFence = new FencePoint(ComputeQueueKind.Copy, 44);

        Assert.AreEqual(ComputeQueueKind.Compute, record.LastComputeRead.Queue);
        Assert.AreEqual(11UL, record.LastComputeRead.Value);
        Assert.AreEqual(ComputeQueueKind.Copy, record.LastCopyRead.Queue);
        Assert.AreEqual(22UL, record.LastCopyRead.Value);
        Assert.AreEqual(ComputeQueueKind.Compute, record.LastWrite.Queue);
        Assert.AreEqual(33UL, record.LastWrite.Value);
        Assert.AreEqual(ComputeQueueKind.Copy, record.RetirementFence.Queue);
        Assert.AreEqual(44UL, record.RetirementFence.Value);

        record.LastCopyRead = FencePoint.None;

        Assert.AreEqual(ComputeQueueKind.None, record.LastCopyRead.Queue);
        Assert.AreEqual(0UL, record.LastCopyRead.Value);
        Assert.AreEqual(ComputeQueueKind.Compute, record.LastComputeRead.Queue);
        Assert.AreEqual(ComputeQueueKind.Compute, record.LastWrite.Queue);
        Assert.AreEqual(ComputeQueueKind.Copy, record.RetirementFence.Queue);
    }

    [TestMethod]
    public void KeepsEveryPackedFieldWrittenWhenTheyAreAssignedConcurrently()
    {
        for (int round = 0; round < RoundCount; round++)
        {
            GenerationOwner owner = new();

            _ = RunSimultaneously(index =>
            {
                switch (index)
                {
                    case 0:
                        owner.GetResourceRecord(0).Lifecycle = ResourceGenerationState.Releasing;
                        break;
                    case 1:
                        owner.GetResourceRecord(0).Ownership = ExternalOwnershipState.ReleaseSignalEnqueued;
                        break;
                    case 2:
                        owner.GetResourceRecord(0).D3D12State = TrackedResourceState.CopySource;
                        break;
                    case 3:
                        owner.GetResourceRecord(0).Placement = MemoryPlacement.Local;
                        break;
                    case 4:
                        owner.GetResourceRecord(0).Recovery = ComputeResourceRecovery.CapacityOnly;
                        break;
                    case 5:
                        owner.GetResourceRecord(0).ReleaseAuthority = ResourceReleaseAuthority.DomainTeardown;
                        break;
                    case 6:
                        owner.GetResourceRecord(0).LastComputeRead = new FencePoint(ComputeQueueKind.Copy, 7);
                        break;
                    default:
                        owner.GetResourceRecord(0).RetirementFence = new FencePoint(ComputeQueueKind.Compute, 9);
                        break;
                }

                return false;
            });

            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(0);

            Assert.AreEqual(ResourceGenerationState.Releasing, record.ReadLifecycle());
            Assert.AreEqual(ExternalOwnershipState.ReleaseSignalEnqueued, record.ReadOwnership());
            Assert.AreEqual(TrackedResourceState.CopySource, record.D3D12State);
            Assert.AreEqual(MemoryPlacement.Local, record.Placement);
            Assert.AreEqual(ComputeResourceRecovery.CapacityOnly, record.Recovery);
            Assert.AreEqual(ResourceReleaseAuthority.DomainTeardown, record.ReleaseAuthority);
            Assert.AreEqual(ComputeQueueKind.Copy, record.LastComputeRead.Queue);
            Assert.AreEqual(7UL, record.LastComputeRead.Value);
            Assert.AreEqual(ComputeQueueKind.Compute, record.RetirementFence.Queue);
            Assert.AreEqual(9UL, record.RetirementFence.Value);
            Assert.IsTrue(record.IsExternalObjectsReleased);
        }
    }
}
