using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PreparedGenerationRollbackTests
{
    private sealed class GenerationOwner : IResourceGenerationOwner
    {
        private readonly ResourceGenerationRecord[] records;

        public GenerationOwner(ulong setId, int resourceCount, int externalObjectsReleased)
        {
            SetId = new ResourceGenerationSetId(setId);
            this.records = new ResourceGenerationRecord[resourceCount];

            for (int i = 0; i < resourceCount; i++)
            {
                this.records[i] = new ResourceGenerationRecord
                {
                    Id = new ResourceGenerationId((ulong)i + 1),
                    Lifecycle = ResourceGenerationState.Active,
                    ExternalObjectsReleased = externalObjectsReleased
                };
            }
        }

        public ResourceGenerationSetId SetId { get; }

        public int ResourceCount => this.records.Length;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.records[resourceOrdinal];
        }
    }

    private static ResourceGenerationSetHandle Handle(int resourceCount, int externalObjectsReleased)
    {
        return new ResourceGenerationSetHandle(new GenerationOwner(7, resourceCount, externalObjectsReleased));
    }

    private static void AssertAllLifecycles(ResourceGenerationSetHandle handle, ResourceGenerationState expected)
    {
        for (int i = 0; i < handle.Owner.ResourceCount; i++)
        {
            Assert.AreEqual(expected, handle.Owner.GetResourceRecord(i).Lifecycle);
        }
    }

    [TestMethod]
    public void IgnoresEmptyHandle()
    {
        PreparedGenerationRollback.RollbackUnpublished(default);
    }

    [TestMethod]
    public void PromotesUnreferencedComputeGenerationToRetiredReady()
    {
        ResourceGenerationSetHandle prepared = Handle(resourceCount: 3, externalObjectsReleased: 1);

        PreparedGenerationRollback.RollbackUnpublished(prepared);

        AssertAllLifecycles(prepared, ResourceGenerationState.RetiredReady);
    }

    [TestMethod]
    public void KeepsExternalGenerationPendingUntilExternalObjectsAreReleased()
    {
        ResourceGenerationSetHandle prepared = Handle(resourceCount: 2, externalObjectsReleased: 0);

        PreparedGenerationRollback.RollbackUnpublished(prepared);

        AssertAllLifecycles(prepared, ResourceGenerationState.RetiredPending);
    }

    [TestMethod]
    public void KeepsReferencedGenerationPending()
    {
        ResourceGenerationSetHandle prepared = Handle(resourceCount: 1, externalObjectsReleased: 1);

        Assert.IsTrue(prepared.Owner.GetResourceRecord(0).TryAcquireCpuReference());

        PreparedGenerationRollback.RollbackUnpublished(prepared);

        AssertAllLifecycles(prepared, ResourceGenerationState.RetiredPending);
    }

    [TestMethod]
    public void IsIdempotent()
    {
        ResourceGenerationSetHandle prepared = Handle(resourceCount: 2, externalObjectsReleased: 1);

        PreparedGenerationRollback.RollbackUnpublished(prepared);
        PreparedGenerationRollback.RollbackUnpublished(prepared);

        AssertAllLifecycles(prepared, ResourceGenerationState.RetiredReady);
    }
}
