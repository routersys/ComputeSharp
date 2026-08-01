using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class OwnedResourceSlotDeclarationAnalyzerTests
{
    private const string Preamble = """
        using ComputeWeave;
        using ComputeWeave.Resources;

        namespace Ukiyoe;
        """;

    private static string Host(string members)
    {
        return $$"""
            {{Preamble}}

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

            {{members}}

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                }
            }
            """;
    }

    private static string Group()
    {
        return $$"""
            {{Preamble}}

            [ComputeResourceGroup]
            public sealed partial class GridResources
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                internal ReadWriteBuffer<int> Cells { get; } = null!;
            }
            """;
    }

    [TestMethod]
    public void AcceptsOwnedSlotsWithARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                    private readonly ComputeResourceGroupSlot<GridResources> grid = new();
                """), Group()],
            "AcceptsOwnedSlots");
    }

    [TestMethod]
    public void AcceptsBorrowedResourcesWithoutARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly ReadWriteBuffer<int> borrowed;
                """)],
            "AcceptsBorrowedResources");
    }

    [TestMethod]
    public void DetectsAnOwnedResourceThatIsNotDeclaredThroughASlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ReadWriteBuffer<int> owned;
                """)],
            "DetectsOwnedResourceWithoutSlot",
            "CMPW0075");
    }

    [TestMethod]
    public void DetectsAnOwnedResourceCollection()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly System.Collections.Generic.List<ComputeResourceSlot<ReadWriteBuffer<int>>> owned = new();
                """)],
            "DetectsOwnedResourceCollection",
            "CMPW0092");
    }

    [TestMethod]
    public void DetectsAnOwnedSlotWithoutARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();
                """)],
            "DetectsSlotWithoutRecovery",
            "CMPW0101");
    }

    [TestMethod]
    public void DetectsAnOwnedGroupSlotWithoutARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly ComputeResourceGroupSlot<GridResources> grid = new();
                """), Group()],
            "DetectsGroupSlotWithoutRecovery",
            "CMPW0101");
    }

    [TestMethod]
    public void AcceptsResourceGroupMembersWithoutARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Group()],
            "AcceptsGroupMembers");
    }

    [TestMethod]
    public void DetectsAResourceGroupMemberDeclaringARecoveryContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [$$"""
                {{Preamble}}

                [ComputeResourceGroup]
                public sealed partial class GridResources
                {
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    internal ReadWriteBuffer<int> Cells { get; } = null!;
                }
                """],
            "DetectsGroupMemberRecovery",
            "CMPW0108");
    }

    [TestMethod]
    public void AcceptsAHostWithoutOwnedDisposableFields()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedOwnedDisposableFieldAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly ReadWriteBuffer<int> borrowed;

                    private readonly int seed;
                """)],
            "AcceptsHostWithoutDisposables");
    }

    [TestMethod]
    public void DetectsAHostOwningADisposableFieldOtherThanASlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedOwnedDisposableFieldAnalyzer(),
            [Host("""
                    private readonly System.IO.MemoryStream stream = new();
                """)],
            "DetectsOwnedDisposableField",
            "CMPW0097");
    }

    [TestMethod]
    public void AcceptsOwnedSlotsWithABufferOrTexture2DPlan()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.Read, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadOnlyTexture2D<float>> mask = new();
                """)],
            "AcceptsSupportedPlans");
    }

    [TestMethod]
    public void DetectsAnOwnedSlotWithoutAResourcePlan()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteTexture3D<float>> volume = new();
                """)],
            "DetectsSlotWithoutPlan",
            "CMPW0102");
    }

    [TestMethod]
    public void DetectsAResourceGroupMemberWithoutAResourcePlan()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [$$"""
                {{Preamble}}

                [ComputeResourceGroup]
                public sealed partial class GridResources
                {
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    internal ReadWriteTexture1D<float> Line { get; } = null!;
                }
                """],
            "DetectsGroupMemberWithoutPlan",
            "CMPW0102");
    }

    [TestMethod]
    public void DetectsAnInternalResourceThatIsNotAGraphicsResource()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly object borrowed;
                """)],
            "DetectsInternalResourceContract",
            "CMPW0071");
    }

    [TestMethod]
    public void DetectsAnOwnedSlotWithAnAssignedInitializer()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = null!;
                """)],
            "DetectsAssignedSlotInitializer",
            "CMPW0087");
    }

    [TestMethod]
    public void DetectsAnOwnedSlotWithoutAnInitializer()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index;
                """)],
            "DetectsMissingSlotInitializer",
            "CMPW0087");
    }

    [TestMethod]
    public void DetectsADynamicResourceCollectionOnAHost()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    private readonly ReadWriteBuffer<int>[] resources;
                """)],
            "DetectsHostResourceCollection",
            "CMPW0092");
    }

    [TestMethod]
    public void DetectsADynamicResourceCollectionOnAResourceGroup()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceDeclarationAnalyzer(),
            [$$"""
                {{Preamble}}

                [ComputeResourceGroup]
                public sealed partial class GridResources
                {
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    internal System.Collections.Generic.List<ReadWriteBuffer<int>> Cells { get; } = null!;
                }
                """],
            "DetectsGroupResourceCollection",
            "CMPW0092");
    }
}
