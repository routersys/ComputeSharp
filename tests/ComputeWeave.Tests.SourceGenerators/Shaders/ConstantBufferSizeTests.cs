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
