using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class DuplicateContractAttributeAnalyzerTests
{
    private const string SharedTextureContract = """
        [ComputeSharedTexture(
            ComputeResourceResizePolicy.Exact,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost)]
        """;

    private static string Container(string members)
    {
        return $$"""
            using ComputeWeave;
            using ComputeWeave.Resources;

            namespace Ukiyoe;

            public sealed partial class Container
            {
            {{members}}
            }
            """;
    }

    [TestMethod]
    public void AcceptsAMemberDeclaringOnlyASharedTextureContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new DuplicateContractAttributeAnalyzer(),
            [Container($"""
                    {SharedTextureContract}
                    private readonly int output;
                """)],
            "AcceptsSharedTextureContract");
    }

    [TestMethod]
    public void AcceptsAMemberDeclaringOnlyAPipelineResourceContract()
    {
        AnalyzerHelper.AssertDiagnostics(
            new DuplicateContractAttributeAnalyzer(),
            [Container("""
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                    private readonly int output;
                """)],
            "AcceptsPipelineResourceContract");
    }

    [TestMethod]
    public void DetectsAMemberDeclaringBothContractAttributes()
    {
        AnalyzerHelper.AssertDiagnostics(
            new DuplicateContractAttributeAnalyzer(),
            [Container($"""
                    {SharedTextureContract}
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                    private readonly int output;
                """)],
            "DetectsDuplicateContract",
            "CMPW0089");
    }
}
