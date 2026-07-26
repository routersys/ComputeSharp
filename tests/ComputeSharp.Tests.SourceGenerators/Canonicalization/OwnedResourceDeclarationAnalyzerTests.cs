using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class OwnedResourceDeclarationAnalyzerTests
{
    private const string Preamble = """
        using ComputeSharp;
        using ComputeSharp.Resources;

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

    private static string Group(string members)
    {
        return $$"""
            {{Preamble}}

            [ComputeResourceGroup]
            public sealed partial class GridResources
            {
            {{members}}
            }
            """;
    }

    [TestMethod]
    public void AcceptsOwnedSlotsMatchingTheirAccessContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceTypeAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.Read, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadOnlyTexture2D<float>> mask = new();
                """)],
            "AcceptsMatchingSlots");
    }

    [TestMethod]
    public void AcceptsOwnedSlotsDeclaringABaseResourceType()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceTypeAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<StructuredBuffer<int>> index = new();
                """)],
            "AcceptsBaseSlotType");
    }

    [TestMethod]
    public void RejectsAReadOnlySlotTypeWithAReadWriteAccessContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceTypeAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadOnlyBuffer<int>> index = new();
                """)],
            "RejectsReadOnlySlotType",
            "CMPS0094");
    }

    [TestMethod]
    public void RejectsAReadWriteSlotTypeWithAReadAccessContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceTypeAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.Read, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteTexture2D<float>> mask = new();
                """)],
            "RejectsReadWriteSlotType",
            "CMPS0094");
    }

    [TestMethod]
    public void RejectsAResourceGroupMemberTypeWithAMismatchedAccessContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceTypeAnalyzer(),
            [Group("""
                    [ComputePipelineResource(ComputeResourceAccess.Read)]
                    public ReadWriteBuffer<int> Cells { get; } = null!;

                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    public ReadOnlyTexture2D<float> Weights { get; } = null!;
                """)],
            "RejectsGroupMemberType",
            "CMPS0107",
            "CMPS0107");
    }

    [TestMethod]
    public void AcceptsOwnedMembersWithDistinctCanonicalNames()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> mask = new();
                """)],
            "AcceptsDistinctCanonicalNames");
    }

    [TestMethod]
    public void RejectsAnOwnedMemberWithoutACanonicalName()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> _ = new();
                """)],
            "RejectsEmptyCanonicalName",
            "CMPS0104");
    }

    [TestMethod]
    public void RejectsOwnedMembersSharingACanonicalName()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> _index = new();
                """)],
            "RejectsSharedCanonicalName",
            "CMPS0104",
            "CMPS0104");
    }

    [TestMethod]
    public void RejectsAnOwnedMemberConflictingWithADeclaredPlanType()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    public readonly struct IndexPlan
                    {
                    }
                """)],
            "RejectsDeclaredPlanType",
            "CMPS0104");
    }

    [TestMethod]
    public void RejectsAnOwnedMemberConflictingWithADeclaredTypedMethod()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Host("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                    public void GetIndexComputeBinding()
                    {
                    }
                """)],
            "RejectsDeclaredTypedMethod",
            "CMPS0104");
    }

    [TestMethod]
    public void RejectsResourceGroupMembersConflictingWithADeclaredPlanType()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPlanSignatureAnalyzer(),
            [Group("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                    public ReadWriteBuffer<int> Cells { get; } = null!;

                    public readonly struct Plan
                    {
                    }
                """)],
            "RejectsGroupPlanType",
            "CMPS0104");
    }
}
