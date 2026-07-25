using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineCanonicalOrderingTests
{
    private static PipelineContractInfo Pipeline(string methodMetadataName, string canonicalSignature, PipelineFlags flags = PipelineFlags.None)
    {
        return new PipelineContractInfo(
            uint.MaxValue,
            methodMetadataName,
            canonicalSignature,
            flags,
            0,
            0,
            ImmutableArray<ResourceContractInfo>.Empty,
            ImmutableArray<ResourceContractInfo>.Empty);
    }

    private static ResourceContractInfo Resource(string resourceTypeMetadataName)
    {
        return new ResourceContractInfo(
            uint.MaxValue,
            resourceTypeMetadataName,
            ComputeResourceAccess.Read,
            ComputeResourceSharing.Internal,
            ComputeResourceAliasing.Disallow,
            ResourceOwnershipKind.Borrowed,
            false,
            0,
            0);
    }

    private static OwnedSlotContractInfo Slot(string memberMetadataName)
    {
        return new OwnedSlotContractInfo(
            uint.MaxValue,
            memberMetadataName,
            "ComputeSharp.ReadWriteBuffer`1[System.Int32]",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Buffer,
            ComputeResourceRecovery.Discardable,
            ImmutableArray<ResourcePlanFieldContractInfo>.Empty);
    }

    private static SharedTextureContractInfo SharedTexture(string memberMetadataName)
    {
        return new SharedTextureContractInfo(
            uint.MaxValue,
            memberMetadataName,
            "ComputeSharp.Interop.ComputeSharedTexture2D`1",
            ComputeResourceResizePolicy.Exact,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost);
    }

    private static ResourcePlanFieldContractInfo PlanField(uint slotResourceIndex, ResourcePlanDimensionKind dimensionKind)
    {
        return new ResourcePlanFieldContractInfo(
            uint.MaxValue,
            slotResourceIndex,
            "Member",
            "ComputeSharp.ReadWriteBuffer`1[System.Int32]",
            "memberLength",
            dimensionKind);
    }

    [TestMethod]
    public void ComparesHostsByOrdinalMetadataName()
    {
        Assert.IsTrue(PipelineCanonicalOrdering.CompareHosts("Ukiyoe.A", "Ukiyoe.B") < 0);
        Assert.IsTrue(PipelineCanonicalOrdering.CompareHosts("Ukiyoe.B", "Ukiyoe.A") > 0);
        Assert.AreEqual(0, PipelineCanonicalOrdering.CompareHosts("Ukiyoe.A", "Ukiyoe.A"));
        Assert.IsTrue(PipelineCanonicalOrdering.CompareHosts("Ukiyoe.Z", "Ukiyoe.a") < 0);
    }

    [TestMethod]
    public void OrdersPipelinesBySignatureAndAssignsOrdinals()
    {
        EquatableArray<PipelineContractInfo> pipelines = ImmutableArray.Create(
            Pipeline("Second", "H|Second|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext"),
            Pipeline("First", "H|First|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext"));

        Assert.IsTrue(PipelineCanonicalOrdering.TryOrderPipelines(pipelines, out EquatableArray<PipelineContractInfo> ordered));

        ImmutableArray<PipelineContractInfo> items = ordered.AsImmutableArray();

        Assert.AreEqual(2, items.Length);
        Assert.AreEqual("First", items[0].MethodMetadataName);
        Assert.AreEqual(0u, items[0].Ordinal);
        Assert.AreEqual("Second", items[1].MethodMetadataName);
        Assert.AreEqual(1u, items[1].Ordinal);
    }

    [TestMethod]
    public void ProducesSameOrderForReversedDiscoveryOrder()
    {
        PipelineContractInfo first = Pipeline("A", "H|A|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext");
        PipelineContractInfo second = Pipeline("B", "H|B|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext");
        PipelineContractInfo third = Pipeline("C", "H|C|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext");

        Assert.IsTrue(PipelineCanonicalOrdering.TryOrderPipelines(ImmutableArray.Create(first, second, third), out EquatableArray<PipelineContractInfo> forward));
        Assert.IsTrue(PipelineCanonicalOrdering.TryOrderPipelines(ImmutableArray.Create(third, second, first), out EquatableArray<PipelineContractInfo> reversed));

        Assert.AreEqual(forward, reversed);
    }

    [TestMethod]
    public void RejectsDuplicateCanonicalSignature()
    {
        const string Signature = "H|A|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext";

        Assert.IsFalse(PipelineCanonicalOrdering.TryOrderPipelines(
            ImmutableArray.Create(Pipeline("A", Signature), Pipeline("A", Signature)),
            out _));
    }

    [TestMethod]
    public void OrdersSlotsByMemberMetadataName()
    {
        EquatableArray<OwnedSlotContractInfo> ordered = PipelineCanonicalOrdering.OrderSlots(
            ImmutableArray.Create(Slot("Output"), Slot("Grid"), Slot("Source")));

        ImmutableArray<OwnedSlotContractInfo> items = ordered.AsImmutableArray();

        Assert.AreEqual("Grid", items[0].MemberMetadataName);
        Assert.AreEqual(0u, items[0].Ordinal);
        Assert.AreEqual("Output", items[1].MemberMetadataName);
        Assert.AreEqual(1u, items[1].Ordinal);
        Assert.AreEqual("Source", items[2].MemberMetadataName);
        Assert.AreEqual(2u, items[2].Ordinal);
    }

    [TestMethod]
    public void OrdersSharedTexturesByMemberMetadataName()
    {
        EquatableArray<SharedTextureContractInfo> ordered = PipelineCanonicalOrdering.OrderSharedTextures(
            ImmutableArray.Create(SharedTexture("Source"), SharedTexture("Output")));

        ImmutableArray<SharedTextureContractInfo> items = ordered.AsImmutableArray();

        Assert.AreEqual("Output", items[0].MemberMetadataName);
        Assert.AreEqual(0u, items[0].Ordinal);
        Assert.AreEqual("Source", items[1].MemberMetadataName);
        Assert.AreEqual(1u, items[1].Ordinal);
    }

    [TestMethod]
    public void OrdersPlanFieldsByResourceIndexThenDimension()
    {
        EquatableArray<ResourcePlanFieldContractInfo> ordered = PipelineCanonicalOrdering.OrderPlanFields(
            ImmutableArray.Create(
                PlanField(1, ResourcePlanDimensionKind.Height),
                PlanField(0, ResourcePlanDimensionKind.Length),
                PlanField(1, ResourcePlanDimensionKind.Width)));

        ImmutableArray<ResourcePlanFieldContractInfo> items = ordered.AsImmutableArray();

        Assert.AreEqual(0u, items[0].SlotResourceIndex);
        Assert.AreEqual(ResourcePlanDimensionKind.Length, items[0].DimensionKind);
        Assert.AreEqual(0u, items[0].FieldOrdinal);

        Assert.AreEqual(1u, items[1].SlotResourceIndex);
        Assert.AreEqual(ResourcePlanDimensionKind.Width, items[1].DimensionKind);
        Assert.AreEqual(1u, items[1].FieldOrdinal);

        Assert.AreEqual(1u, items[2].SlotResourceIndex);
        Assert.AreEqual(ResourcePlanDimensionKind.Height, items[2].DimensionKind);
        Assert.AreEqual(2u, items[2].FieldOrdinal);
    }

    [TestMethod]
    public void AssignsContiguousResourceOrdinalsAcrossParametersAndInternals()
    {
        PipelineCanonicalOrdering.AssignResourceOrdinals(
            ImmutableArray.Create(Resource("A"), Resource("B")),
            ImmutableArray.Create(Resource("C")),
            out EquatableArray<ResourceContractInfo> parameters,
            out EquatableArray<ResourceContractInfo> internalResources);

        ImmutableArray<ResourceContractInfo> parameterItems = parameters.AsImmutableArray();
        ImmutableArray<ResourceContractInfo> internalItems = internalResources.AsImmutableArray();

        Assert.AreEqual(0u, parameterItems[0].Ordinal);
        Assert.AreEqual(1u, parameterItems[1].Ordinal);
        Assert.AreEqual(2u, internalItems[0].Ordinal);
    }

    [TestMethod]
    public void DerivesPipelineStructuralRequirements()
    {
        PipelineContractInfo empty = PipelineStructuralRequirements.Derive(Pipeline("A", "A"));

        Assert.AreEqual(0, empty.MaximumTrackedResourceCount);
        Assert.AreEqual(1, empty.MaximumCommandListSegments);

        PipelineContractInfo tracked = PipelineStructuralRequirements.Derive(
            Pipeline("B", "B") with { Parameters = ImmutableArray.Create(Resource("A")) });

        Assert.AreEqual(1, tracked.MaximumTrackedResourceCount);
        Assert.AreEqual(2, tracked.MaximumCommandListSegments);

        PipelineContractInfo interop = PipelineStructuralRequirements.Derive(
            Pipeline("C", "C", PipelineFlags.InteropRoundTrip) with
            {
                Parameters = ImmutableArray.Create(Resource("A")),
                InternalResources = ImmutableArray.Create(Resource("B"))
            });

        Assert.AreEqual(2, interop.MaximumTrackedResourceCount);
        Assert.AreEqual(3, interop.MaximumCommandListSegments);

        PipelineContractInfo interopOnly = PipelineStructuralRequirements.Derive(Pipeline("D", "D", PipelineFlags.InteropRoundTrip));

        Assert.AreEqual(0, interopOnly.MaximumTrackedResourceCount);
        Assert.AreEqual(2, interopOnly.MaximumCommandListSegments);
    }

    [TestMethod]
    public void DerivesHostStructuralRequirementsAsMaximum()
    {
        StructuralRequirementsInfo structural = PipelineStructuralRequirements.Derive(
            ImmutableArray.Create(
                PipelineStructuralRequirements.Derive(Pipeline("A", "A")),
                PipelineStructuralRequirements.Derive(
                    Pipeline("B", "B", PipelineFlags.InteropRoundTrip) with
                    {
                        Parameters = ImmutableArray.Create(Resource("A"), Resource("B"), Resource("C"))
                    })),
            4);

        Assert.AreEqual(3, structural.MaximumTrackedResourceCount);
        Assert.AreEqual(3, structural.MaximumCommandListSegments);
        Assert.AreEqual(4, structural.OwnedSlotCount);
    }
}
