using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class TransientCpuUploadAnalyzerTests
{
    private static string Host(string body)
    {
        return $$"""
            using ComputeSharp;
            using ComputeSharp.Resources;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                private readonly ReadWriteBuffer<int> values;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                private readonly ReadWriteBuffer<int> staging;

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
            {{body}}
                }
            }
            """;
    }

    [TestMethod]
    public void AcceptsAPipelineWithoutCpuUploads()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedTransientCpuUploadAnalyzer(),
            [Host("""
                    context.Clear(this.values);
                """)],
            "AcceptsNoUpload");
    }

    [TestMethod]
    public void AcceptsACopyBetweenGraphicsResources()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedTransientCpuUploadAnalyzer(),
            [Host("""
                    this.values.CopyFrom(this.staging);
                """)],
            "AcceptsResourceCopy");
    }

    [TestMethod]
    public void DetectsAnArrayUploadInsideAPipeline()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedTransientCpuUploadAnalyzer(),
            [Host("""
                    this.values.CopyFrom(new int[16]);
                """)],
            "DetectsArrayUpload",
            "CMPS0105");
    }

    [TestMethod]
    public void DetectsASpanUploadInsideAPipeline()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedTransientCpuUploadAnalyzer(),
            [Host("""
                    System.ReadOnlySpan<int> source = stackalloc int[16];

                    this.values.CopyFrom(source);
                """)],
            "DetectsSpanUpload",
            "CMPS0105");
    }
}
