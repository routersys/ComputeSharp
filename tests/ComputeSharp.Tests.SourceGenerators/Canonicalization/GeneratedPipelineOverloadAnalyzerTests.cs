using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class GeneratedPipelineOverloadAnalyzerTests
{
    private static string Host(string members)
    {
        return $$"""
            using ComputeSharp;
            using ComputeSharp.Resources;

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
            "CMPS0073");
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
            "CMPS0073");
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
            "CMPS0073");
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
            "CMPS0073",
            "CMPS0073");
    }
}
