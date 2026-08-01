using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class ReadWriteParameterAccessAnalyzerTests
{
    private static string Host(string parameters)
    {
        return $$"""
            using ComputeWeave;
            using ComputeWeave.Resources;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipeline]
                private void Run(in ComputeContext context{{parameters}})
                {
                }
            }
            """;
    }

    [TestMethod]
    public void AcceptsAReadWriteParameterWithAReadWriteAccess()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidReadWriteParameterAccessAnalyzer(),
            [Host(", [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> values")],
            "AcceptsReadWriteAccess");
    }

    [TestMethod]
    public void AcceptsAReadOnlyParameterWithAReadAccess()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidReadWriteParameterAccessAnalyzer(),
            [Host(", [ComputeResource(ComputeResourceAccess.Read)] ReadOnlyBuffer<int> values")],
            "AcceptsReadAccess");
    }

    [TestMethod]
    public void DetectsAReadWriteParameterWithAReadAccess()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidReadWriteParameterAccessAnalyzer(),
            [Host(", [ComputeResource(ComputeResourceAccess.Read)] ReadWriteBuffer<int> values")],
            "DetectsReadAccessOnReadWrite",
            "CMPW0098");
    }

    [TestMethod]
    public void DetectsAReadWriteTextureParameterWithAWriteAccess()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidReadWriteParameterAccessAnalyzer(),
            [Host(", [ComputeResource(ComputeResourceAccess.Write)] ReadWriteTexture2D<float> mask")],
            "DetectsWriteAccessOnReadWrite",
            "CMPW0098");
    }
}
