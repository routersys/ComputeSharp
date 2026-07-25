using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class HostResourceCollectorTests
{
    private const string GroupSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        [ComputeResourceGroup]
        public sealed partial class Grid
        {
            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            public ReadWriteBuffer<int> ColorB { get; } = null!;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            public ReadWriteBuffer<int> ColorA { get; } = null!;

            [ComputePipelineResource(ComputeResourceAccess.Read)]
            public ReadWriteTexture2D<Bgra32, Float4> Tangent { get; } = null!;
        }
        """;

    private const string HostUsings = "using ComputeSharp;";

    private static (EquatableArray<OwnedSlotContractInfo> Slots, EquatableArray<UnorderedInternalResourceContract> Resources) Collect(
        string hostSource,
        string assemblyName,
        string hostTypeMetadataName = "Ukiyoe.Host")
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([GroupSource, HostUsings + hostSource], assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol hostSymbol = compilation.GetTypeByMetadataName(hostTypeMetadataName)!;

        Assert.IsNotNull(hostSymbol);
        Assert.IsTrue(HostResourceCollector.TryCollect(
            hostSymbol,
            symbols,
            out EquatableArray<OwnedSlotContractInfo> slots,
            out EquatableArray<UnorderedInternalResourceContract> resources));

        return (slots, resources);
    }

    private static bool TryCollect(string hostSource, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([GroupSource, HostUsings + hostSource], assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol hostSymbol = compilation.GetTypeByMetadataName("Ukiyoe.Host")!;

        Assert.IsNotNull(hostSymbol);

        return HostResourceCollector.TryCollect(hostSymbol, symbols, out _, out _);
    }

    [TestMethod]
    public void CollectsBorrowedFieldWithoutSlot()
    {
        (EquatableArray<OwnedSlotContractInfo> slots, EquatableArray<UnorderedInternalResourceContract> resources) = Collect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private readonly ReadOnlyBuffer<float> weights = null!;
            }
            """,
            "HostResourceCollectorBorrowedTests");

        Assert.AreEqual(0, slots.Length);
        Assert.AreEqual(1, resources.Length);

        UnorderedInternalResourceContract resource = resources.AsImmutableArray()[0];

        Assert.AreEqual("weights", resource.HostMemberMetadataName);
        Assert.IsNull(resource.GroupMemberMetadataName);
        Assert.AreEqual("ComputeSharp.ReadOnlyBuffer`1[System.Single]", resource.ResourceTypeMetadataName);
        Assert.AreEqual(ComputeResourceAccess.Read, resource.Access);
        Assert.AreEqual(ComputeResourceSharing.Internal, resource.Sharing);
        Assert.AreEqual(ComputeResourceAliasing.Disallow, resource.Aliasing);
        Assert.AreEqual(ResourceOwnershipKind.Borrowed, resource.Ownership);
        Assert.AreEqual(0u, resource.SlotResourceIndex);
        Assert.IsNull(resource.SlotKey);
    }

    [TestMethod]
    public void CollectsOwnedSlotWithZeroResourceIndex()
    {
        (EquatableArray<OwnedSlotContractInfo> slots, EquatableArray<UnorderedInternalResourceContract> resources) = Collect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceSlot<ReadWriteTexture2D<Bgra32, Float4>> output = new();
            }
            """,
            "HostResourceCollectorOwnedSlotTests");

        Assert.AreEqual(1, slots.Length);

        OwnedSlotContractInfo slot = slots.AsImmutableArray()[0];

        Assert.AreEqual(0u, slot.Ordinal);
        Assert.AreEqual("output", slot.MemberMetadataName);
        Assert.AreEqual("ComputeSharp.ReadWriteTexture2D`2[ComputeSharp.Bgra32,ComputeSharp.Float4]", slot.ResourceTypeMetadataName);
        Assert.AreEqual(ResourceOwnershipKind.OwnedSlot, slot.Ownership);
        Assert.AreEqual(ResourcePlanKind.Texture2D, slot.PlanKind);
        Assert.AreEqual(ComputeResourceRecovery.Recompute, slot.Recovery);
        Assert.AreEqual(2, slot.PlanFields.Length);

        UnorderedInternalResourceContract resource = resources.AsImmutableArray()[0];

        Assert.AreEqual(ResourceOwnershipKind.OwnedSlot, resource.Ownership);
        Assert.AreEqual(0u, resource.SlotResourceIndex);
        Assert.AreEqual(new SlotContractKey("output"), resource.SlotKey);
        Assert.AreEqual("ComputeSharp.ReadWriteTexture2D`2[ComputeSharp.Bgra32,ComputeSharp.Float4]", resource.ResourceTypeMetadataName);
    }

    [TestMethod]
    public void ExpandsResourceGroupIntoOneContractPerMember()
    {
        (EquatableArray<OwnedSlotContractInfo> slots, EquatableArray<UnorderedInternalResourceContract> resources) = Collect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Grid> grid = new();
            }
            """,
            "HostResourceCollectorGroupTests");

        Assert.AreEqual(1, slots.Length);

        OwnedSlotContractInfo slot = slots.AsImmutableArray()[0];

        Assert.AreEqual("Ukiyoe.Grid", slot.ResourceTypeMetadataName);
        Assert.AreEqual(ResourceOwnershipKind.OwnedGroupSlot, slot.Ownership);
        Assert.AreEqual(ResourcePlanKind.ResourceGroup, slot.PlanKind);
        Assert.AreEqual(4, slot.PlanFields.Length);

        ImmutableArray<ResourcePlanFieldContractInfo> planFields = slot.PlanFields.AsImmutableArray();

        Assert.AreEqual("ColorA", planFields[0].MemberMetadataName);
        Assert.AreEqual(0u, planFields[0].SlotResourceIndex);
        Assert.AreEqual("colorALength", planFields[0].PlanParameterName);
        Assert.AreEqual("ColorB", planFields[1].MemberMetadataName);
        Assert.AreEqual(1u, planFields[1].SlotResourceIndex);
        Assert.AreEqual("Tangent", planFields[2].MemberMetadataName);
        Assert.AreEqual(2u, planFields[2].SlotResourceIndex);
        Assert.AreEqual(ResourcePlanDimensionKind.Width, planFields[2].DimensionKind);
        Assert.AreEqual(ResourcePlanDimensionKind.Height, planFields[3].DimensionKind);

        ImmutableArray<UnorderedInternalResourceContract> items = resources.AsImmutableArray();

        Assert.AreEqual(3, items.Length);
        Assert.AreEqual("ColorA", items[0].GroupMemberMetadataName);
        Assert.AreEqual(0u, items[0].SlotResourceIndex);
        Assert.AreEqual("ColorB", items[1].GroupMemberMetadataName);
        Assert.AreEqual(1u, items[1].SlotResourceIndex);
        Assert.AreEqual("Tangent", items[2].GroupMemberMetadataName);
        Assert.AreEqual(2u, items[2].SlotResourceIndex);

        foreach (UnorderedInternalResourceContract item in items)
        {
            Assert.AreEqual(ResourceOwnershipKind.OwnedGroupSlot, item.Ownership);
            Assert.AreEqual(new SlotContractKey("grid"), item.SlotKey);
            Assert.AreEqual("grid", item.HostMemberMetadataName);
        }
    }

    [TestMethod]
    public void OrdersMembersByMetadataNameRegardlessOfDeclarationOrder()
    {
        (EquatableArray<OwnedSlotContractInfo> forwardSlots, EquatableArray<UnorderedInternalResourceContract> forwardResources) = Collect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> output = new();

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();
            }
            """,
            "HostResourceCollectorForwardTests");

        (EquatableArray<OwnedSlotContractInfo> reversedSlots, EquatableArray<UnorderedInternalResourceContract> reversedResources) = Collect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> output = new();
            }
            """,
            "HostResourceCollectorReversedTests");

        Assert.AreEqual(forwardSlots, reversedSlots);
        Assert.AreEqual(forwardResources, reversedResources);
        Assert.AreEqual("index", forwardSlots.AsImmutableArray()[0].MemberMetadataName);
        Assert.AreEqual(0u, forwardSlots.AsImmutableArray()[0].Ordinal);
        Assert.AreEqual("output", forwardSlots.AsImmutableArray()[1].MemberMetadataName);
        Assert.AreEqual(1u, forwardSlots.AsImmutableArray()[1].Ordinal);
    }

    [TestMethod]
    public void RejectsWritableResourceField()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private ReadOnlyBuffer<float> weights = null!;
            }
            """,
            "HostResourceCollectorWritableTests"));
    }

    [TestMethod]
    public void RejectsBorrowedFieldWithRecovery()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read, ComputeResourceRecovery.Recompute)]
                private readonly ReadOnlyBuffer<float> weights = null!;
            }
            """,
            "HostResourceCollectorBorrowedRecoveryTests"));
    }

    [TestMethod]
    public void RejectsOwnedSlotWithoutRecovery()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();
            }
            """,
            "HostResourceCollectorMissingRecoveryTests"));
    }

    [TestMethod]
    public void RejectsUnsupportedOwnedSlotResourceType()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteTexture3D<int>> volume = new();
            }
            """,
            "HostResourceCollectorUnsupportedSlotTests"));
    }

    [TestMethod]
    public void RejectsGroupSlotWithoutGroupAttribute()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed class NotAGroup
            {
            }

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<NotAGroup> grid = new();
            }
            """,
            "HostResourceCollectorNotAGroupTests"));
    }

    [TestMethod]
    public void RejectsGroupMemberAccessAboveSlotAccess()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Grid> grid = new();
            }
            """,
            "HostResourceCollectorGroupAccessTests"));
    }

    [TestMethod]
    public void RejectsCollidingCanonicalMemberNames()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private readonly ReadOnlyBuffer<float> weights = null!;

                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private readonly ReadOnlyBuffer<float> _weights = null!;
            }
            """,
            "HostResourceCollectorCollisionTests"));
    }

    [TestMethod]
    public void RejectsAnnotatedHostProperty()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private ReadOnlyBuffer<float> Weights { get; } = null!;
            }
            """,
            "HostResourceCollectorPropertyTests"));
    }

    [TestMethod]
    public void RejectsOwnedSlotWithoutInitializer()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index;
            }
            """,
            "HostResourceCollectorSlotInitializerTests"));

        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Grid> grid;
            }
            """,
            "HostResourceCollectorGroupSlotInitializerTests"));
    }

    [TestMethod]
    public void RejectsResourceGroupWithSettableMember()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class Settable
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> Valid { get; } = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> Invalid { get; set; } = null!;
            }

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Settable> group = new();
            }
            """,
            "HostResourceCollectorSettableMemberTests"));
    }

    [TestMethod]
    public void RejectsResourceGroupWithAnnotatedField()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class WithField
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> Valid { get; } = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> Field = null!;
            }

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<WithField> group = new();
            }
            """,
            "HostResourceCollectorAnnotatedFieldTests"));
    }

    [TestMethod]
    public void RejectsResourceGroupWithCollidingCanonicalMemberNames()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class Colliding
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> Color { get; } = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> _Color { get; } = null!;
            }

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Colliding> group = new();
            }
            """,
            "HostResourceCollectorGroupCollisionTests"));
    }

    [TestMethod]
    public void RejectsResourceGroupWithMemberRecovery()
    {
        Assert.IsFalse(TryCollect(
            """

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class WithRecovery
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                public ReadWriteBuffer<int> Color { get; } = null!;
            }

            public sealed partial class Host
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<WithRecovery> group = new();
            }
            """,
            "HostResourceCollectorGroupRecoveryTests"));
    }
}
