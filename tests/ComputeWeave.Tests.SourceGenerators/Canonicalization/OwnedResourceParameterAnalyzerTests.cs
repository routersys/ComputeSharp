using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class OwnedResourceParameterAnalyzerTests
{
    private const string Preamble = """
        using ComputeWeave;
        using ComputeWeave.Resources;

        namespace Ukiyoe;
        """;

    private static string Host(string pipeline)
    {
        return $$"""
            {{Preamble}}

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceGroupSlot<GridResources> grid = new();

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                private readonly ReadWriteBuffer<int> borrowed;

            {{pipeline}}
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
                internal ReadWriteBuffer<int> Cells { get; }
            }
            """;
    }

    [TestMethod]
    public void AcceptsTheResourceAndTheGroupOfEveryOwnedSlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceParameterAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(
                        in ComputeContext context,
                        [ComputeOwnedResource(nameof(index))] ReadWriteBuffer<int> index,
                        [ComputeOwnedResource(nameof(grid))] GridResources grid)
                    {
                    }
                """), Group()],
            "AcceptsOwnedResourceParameters");
    }

    [TestMethod]
    public void RejectsAParameterNamingAMemberThatIsNotAnOwnedSlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceParameterAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(
                        in ComputeContext context,
                        [ComputeOwnedResource(nameof(borrowed))] ReadWriteBuffer<int> borrowed)
                    {
                    }
                """), Group()],
            "RejectsBorrowedField",
            "CMPW0110");
    }

    [TestMethod]
    public void RejectsAParameterOfAMethodThatIsNotAPipeline()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceParameterAnalyzer(),
            [Host("""
                    private void Run([ComputeOwnedResource(nameof(index))] ReadWriteBuffer<int> index)
                    {
                    }
                """), Group()],
            "RejectsNonPipelineMethod",
            "CMPW0110");
    }

    [TestMethod]
    public void RejectsAParameterDeclaringAnotherResourceContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceParameterAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(
                        in ComputeContext context,
                        [ComputeOwnedResource(nameof(index))] [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> index)
                    {
                    }
                """), Group()],
            "RejectsDuplicateContract",
            "CMPW0110");
    }

    [TestMethod]
    public void RejectsAParameterThatDoesNotDeclareTheTypeOfItsSlot()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidOwnedResourceParameterAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(
                        in ComputeContext context,
                        [ComputeOwnedResource(nameof(grid))] ReadWriteBuffer<int> grid)
                    {
                    }
                """), Group()],
            "RejectsMismatchedType",
            "CMPW0111");
    }
}
