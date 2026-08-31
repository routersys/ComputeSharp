using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class ConstantBufferSizeTests
{
    private static string CreateSource(int fieldCount, bool withInaccessibleField)
    {
        string fields = string.Join("\n", Enumerable.Range(0, fieldCount).Select(static i => $"        private readonly float f{i};"));
        string sums = string.Join("\n", Enumerable.Range(0, fieldCount).Select(static i => $"            sum += this.f{i};"));
        string hidden = withInaccessibleField ? "\n        private readonly Hidden hidden;\n" : "";
        string hiddenSum = withInaccessibleField ? "\n            sum += this.hidden.Value;\n" : "";

        return $$"""
            using ComputeWeave;

            namespace Shaders;

            internal partial class Container
            {
                private struct Hidden
                {
                    public float Value;
                }

                [ThreadGroupSize(DefaultThreadGroupSizes.X)]
                [GeneratedComputeShaderDescriptor]
                internal readonly partial struct Shader : IComputeShader
                {
                    private readonly ReadWriteBuffer<float> buffer;

            {{fields}}
            {{hidden}}
                    public void Execute()
                    {
                        float sum = 0;

            {{sums}}
            {{hiddenSum}}
                        this.buffer[ThreadIds.X] = sum;
                    }
                }
            }
            """;
    }

    private static string CreatePixelShaderSource(int fieldCount)
    {
        string fields = string.Join("\n", Enumerable.Range(0, fieldCount).Select(static i => $"    private readonly float f{i};"));
        string sums = string.Join("\n", Enumerable.Range(0, fieldCount).Select(static i => $"        sum += this.f{i};"));

        return $$"""
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct PixelShader : IComputeShader<Float4>
            {
            {{fields}}

                public Float4 Execute()
                {
                    float sum = 0;

            {{sums}}

                    return new Float4(sum, 0, 0, 1);
                }
            }
            """;
    }

    [TestMethod]
    public void APixelShaderAtTheLimitIsNotReported()
    {
        // Two artificial values, 61 captured values and the implicit output texture make exactly 64
        AnalyzerHelper.AssertDiagnostics(
            new ExcedeedComputeShaderDispatchDataSizeAnalyzer(),
            [CreatePixelShaderSource(61)],
            "PixelShaderDispatchDataSizeAtLimitTests");
    }

    [TestMethod]
    public void APixelShaderPastTheLimitIsReported()
    {
        // One more captured value takes the root signature to 65, which D3D12 refuses
        AnalyzerHelper.AssertDiagnostics(
            new ExcedeedComputeShaderDispatchDataSizeAnalyzer(),
            [CreatePixelShaderSource(62)],
            "PixelShaderDispatchDataSizeExceededTests",
            "CMPW0041");
    }

    [TestMethod]
    public void AFieldOfAnInaccessibleTypeIsNotCountedTowardsTheSize()
    {
        AnalyzerHelper.AssertDiagnostics(
            new ExcedeedComputeShaderDispatchDataSizeAnalyzer(),
            [CreateSource(60, withInaccessibleField: true)],
            "ConstantBufferSizeWithInaccessibleFieldTests");
    }

    [TestMethod]
    public void TheSizeIsStillReportedWhenItIsActuallyExceeded()
    {
        AnalyzerHelper.AssertDiagnostics(
            new ExcedeedComputeShaderDispatchDataSizeAnalyzer(),
            [CreateSource(61, withInaccessibleField: false)],
            "ConstantBufferSizeExceededTests",
            "CMPW0041");
    }
}
