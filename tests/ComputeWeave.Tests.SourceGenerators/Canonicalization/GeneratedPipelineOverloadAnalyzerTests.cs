using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class GeneratedPipelineOverloadAnalyzerTests
{
    private static string Host(string members)
    {
        return $$"""
            using ComputeWeave;
            using ComputeWeave.Resources;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

            {{members}}
            }
            """;
    }

    [TestMethod]
    public void AcceptsAPipelineMethodWithoutConflicts()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context, int seed)
                    {
                    }
                """)],
            "AcceptsPipelineMethod");
    }

    [TestMethod]
    public void AcceptsAnOverloadWithAnotherSignature()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context, int seed)
                    {
                    }

                    private void Run(float seed)
                    {
                    }
                """)],
            "AcceptsOtherSignature");
    }

    [TestMethod]
    public void AcceptsTwoPipelineMethodsWithDistinctCanonicalNames()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context)
                    {
                    }

                    [ComputePipeline]
                    private void Draw(in ComputeContext context)
                    {
                    }
                """)],
            "AcceptsDistinctPipelines");
    }

    [TestMethod]
    public void DetectsAMethodWithTheGeneratedOverloadSignature()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context, int seed)
                    {
                    }

                    private void Run(int seed)
                    {
                    }
                """)],
            "DetectsOverloadConflict",
            "CMPW0073");
    }

    [TestMethod]
    public void DetectsAMethodDifferingOnlyByAParameterModifier()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context, int seed)
                    {
                    }

                    private void Run(ref int seed)
                    {
                    }
                """)],
            "DetectsModifierOnlyConflict",
            "CMPW0073");
    }

    [TestMethod]
    public void DetectsADeclaredInvocationType()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context)
                    {
                    }

                    private readonly struct RunInvocation
                    {
                    }
                """)],
            "DetectsInvocationTypeConflict",
            "CMPW0073");
    }

    [TestMethod]
    public void DetectsTwoPipelineMethodsSharingACanonicalName()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedPipelineOverloadAnalyzer(),
            [Host("""
                    [ComputePipeline]
                    private void Run(in ComputeContext context)
                    {
                    }

                    [ComputePipeline]
                    private void _Run(in ComputeContext context, int seed)
                    {
                    }
                """)],
            "DetectsSharedCanonicalName",
            "CMPW0073",
            "CMPW0073");
    }
}
