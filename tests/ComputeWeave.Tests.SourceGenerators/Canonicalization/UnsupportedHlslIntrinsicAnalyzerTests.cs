using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class UnsupportedHlslIntrinsicAnalyzerTests
{
    private static string Shader(string body)
    {
        return $$"""
            using ComputeWeave;

            namespace Ukiyoe;

            public readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
            {{body}}
                }
            }
            """;
    }

    [TestMethod]
    public void DetectsTheIntrinsicsThatNoComputeShaderModelAllows()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedHlslIntrinsicAnalyzer(),
            [Shader("""
                    Hlsl.Abort();
                    Hlsl.Clip(this.buffer[0]);
                """)],
            "DetectsUnsupportedStageIntrinsics",
            "CMPW0112",
            "CMPW0112");
    }

    [TestMethod]
    public void DetectsTheDerivativeIntrinsics()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedHlslIntrinsicAnalyzer(),
            [Shader("""
                    this.buffer[0] = Hlsl.DerivativeOfDx(this.buffer[1]);
                    this.buffer[2] = Hlsl.DerivativeOfDxHighPrecision(this.buffer[3]);
                    this.buffer[4] = Hlsl.DerivativeOfDxLowPrecision(this.buffer[5]);
                    this.buffer[6] = Hlsl.DerivativeOfDy(this.buffer[7]);
                    this.buffer[8] = Hlsl.DerivativeOfDyHighPrecision(this.buffer[9]);
                    this.buffer[10] = Hlsl.DerivativeOfDyLowPrecision(this.buffer[11]);
                    this.buffer[12] = Hlsl.Fwidth(this.buffer[13]);
                """)],
            "DetectsDerivativeIntrinsics",
            "CMPW0112",
            "CMPW0112",
            "CMPW0112",
            "CMPW0112",
            "CMPW0112",
            "CMPW0112",
            "CMPW0112");
    }

    [TestMethod]
    public void AcceptsAnIntrinsicThatAComputeShaderCanUse()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedHlslIntrinsicAnalyzer(),
            [Shader("""
                    this.buffer[0] = Hlsl.Sqrt(this.buffer[1]);
                    this.buffer[2] = Hlsl.SmoothStep(0, 1, this.buffer[3]);
                """)],
            "AcceptsSupportedIntrinsics");
    }

    [TestMethod]
    public void AcceptsAMethodOfTheSameNameOnAnotherType()
    {
        AnalyzerHelper.AssertDiagnostics(
            new UnsupportedHlslIntrinsicAnalyzer(),
            [
                """
                namespace Ukiyoe;

                public static class Other
                {
                    public static float Fwidth(float x) => x;

                    public static void Abort()
                    {
                    }
                }
                """,
                Shader("""
                        this.buffer[0] = Other.Fwidth(this.buffer[1]);
                        Other.Abort();
                    """)
            ],
            "AcceptsAnotherType");
    }
}
