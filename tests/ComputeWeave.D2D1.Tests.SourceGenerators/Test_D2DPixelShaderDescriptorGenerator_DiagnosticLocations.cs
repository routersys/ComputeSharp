using System;
using System.Linq;
using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// Where the diagnostics of the shader compilation are reported. The pixel shader generator reaches the same
/// shared model as the compute one, so the two halves are pinned separately rather than through one of them.
/// </summary>
[TestClass]
public class Test_D2DPixelShaderDescriptorGenerator_DiagnosticLocations
{
    private const string MissingAttributeSource = """
        using ComputeWeave;
        using ComputeWeave.D2D1;
        using float4 = global::ComputeWeave.Float4;

        namespace MyNamespace;

        [D2DInputCount(0)]
        [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
        [D2DGeneratedPixelShaderDescriptor]
        internal readonly partial struct MyShader : ID2D1PixelShader
        {
            private readonly float time;

            public float4 Execute()
            {
                return (float)(time * 2.0);
            }
        }
        """;

    private const string UnnecessaryAttributeSource = """
        using ComputeWeave;
        using ComputeWeave.D2D1;
        using float4 = global::ComputeWeave.Float4;

        namespace MyNamespace;

        [D2DInputCount(0)]
        [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
        [D2DRequiresDoublePrecisionSupport]
        [D2DGeneratedPixelShaderDescriptor]
        internal readonly partial struct MyShader : ID2D1PixelShader
        {
            private readonly float time;

            public float4 Execute()
            {
                return (float)(time * 2.0f);
            }
        }
        """;

    private const string CompilerRefusedSource = """
        using ComputeWeave;
        using ComputeWeave.D2D1;
        using float4 = global::ComputeWeave.Float4;

        namespace MyNamespace;

        [D2DInputCount(0)]
        [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
        [D2DGeneratedPixelShaderDescriptor]
        internal readonly partial struct MyShader : ID2D1PixelShader
        {
            private readonly float time;

            public float4 Execute()
            {
                static int Fib(int n) => n <= 1 ? n : Fib(n - 1) + Fib(n - 2);

                return time + Fib(3);
            }
        }
        """;

    [TestMethod]
    public void AShaderTheCompilerRefusesIsReportedAtTheShader()
    {
        AssertReportedAt(CompilerRefusedSource, "CMPWD2D0034", "internal readonly partial struct MyShader");
    }

    [TestMethod]
    public void MissingD2DRequiresDoublePrecisionSupportAttributeIsReportedAtTheShader()
    {
        AssertReportedAt(MissingAttributeSource, "CMPWD2D0080", "internal readonly partial struct MyShader");
    }

    [TestMethod]
    public void UnnecessaryD2DRequiresDoublePrecisionSupportAttributeIsReportedAtTheAttribute()
    {
        AssertReportedAt(UnnecessaryAttributeSource, "CMPWD2D0081", "[D2DRequiresDoublePrecisionSupport]");
    }

    /// <summary>
    /// Where a diagnostic lands is a line and a column to the author, and a tree to the compiler: a
    /// <c>#pragma</c> and the analyzer configuration entries for a file are applied through the tree.
    /// </summary>
    [TestMethod]
    public void UnnecessaryD2DRequiresDoublePrecisionSupportAttributeIsReportedInsideTheTree()
    {
        Diagnostic diagnostic = CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>
            .GetReportedDiagnostics(UnnecessaryAttributeSource, "Shader.cs")
            .Single(diagnostic => diagnostic.Id == "CMPWD2D0081");

        Assert.AreEqual(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.AreEqual("Shader.cs", diagnostic.Location.SourceTree!.FilePath);
    }

    private static void AssertReportedAt(string source, string expectedId, string expectedLineText)
    {
        Diagnostic diagnostic = CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>
            .GetReportedDiagnostics(source)
            .Single(diagnostic => diagnostic.Id == expectedId);

        Assert.AreNotEqual(Location.None, diagnostic.Location, $"{expectedId} is reported with no position at all.");

        string[] lines = source.Split('\n');
        int expectedLine = Array.FindIndex(lines, line => line.Contains(expectedLineText));

        Assert.AreNotEqual(-1, expectedLine, $"the source does not contain {expectedLineText}");
        Assert.AreEqual(expectedLine, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }
}
