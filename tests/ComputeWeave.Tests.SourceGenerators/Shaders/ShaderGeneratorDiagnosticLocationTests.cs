using System;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Where the diagnostics of the shader compilation are reported.
/// </summary>
/// <remarks>
/// Four diagnostics of this generator are synthesized after the transform node, from a location captured by
/// value rather than from the symbol, and one of the four is the one the coverage table records as reported only
/// when the compiler fails without a message, which no input produces. The rows beside this one assert
/// identifiers alone, so a position that stops being reported at all would leave every one of them green.
/// </remarks>
[TestClass]
public class ShaderGeneratorDiagnosticLocationTests
{
    private const string MissingAttributeSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;
            private readonly double factor;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = (float)(this.buffer[ThreadIds.X] * this.factor);
            }
        }
        """;

    private const string UnnecessaryAttributeSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [RequiresDoublePrecisionSupport]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;
            private readonly float factor;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = (float)(this.buffer[ThreadIds.X] * this.factor);
            }
        }
        """;

    private const string CompilerRefusedSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            [GroupShared(16384)]
            private static readonly float[] cache;

            public void Execute()
            {
                cache[ThreadIds.X] = 1;

                this.buffer[ThreadIds.X] = cache[ThreadIds.X];
            }
        }
        """;

    [TestMethod]
    public void AShaderTheCompilerRefusesIsReportedAtTheShader()
    {
        AssertReportedAt(CompilerRefusedSource, "CMPW0046", "internal readonly partial struct Shader");
    }

    [TestMethod]
    public void AMissingDoublePrecisionSupportAttributeIsReportedAtTheShader()
    {
        AssertReportedAt(MissingAttributeSource, "CMPW0064", "internal readonly partial struct Shader");
    }

    [TestMethod]
    public void AnUnnecessaryDoublePrecisionSupportAttributeIsReportedAtTheAttribute()
    {
        AssertReportedAt(UnnecessaryAttributeSource, "CMPW0065", "[RequiresDoublePrecisionSupport]");
    }

    /// <summary>
    /// Where a diagnostic lands is a line and a column to the author, and a tree to the compiler: a
    /// <c>#pragma</c> and the analyzer configuration entries for a file are applied through the tree.
    /// </summary>
    [TestMethod]
    public void AnUnnecessaryDoublePrecisionSupportAttributeIsReportedInsideTheTree()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(UnnecessaryAttributeSource, "ShaderDiagnosticLocationInTree");
        SyntaxTree tree = compilation.SyntaxTrees.Single();
        SyntaxTree named = CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)tree.Options, "Shader.cs");

        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation.ReplaceSyntaxTree(tree, named)).GetRunResult().Results[0];

        Diagnostic diagnostic = result.Diagnostics.Single(diagnostic => diagnostic.Id == "CMPW0065");

        Assert.AreEqual(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.AreSame(named, diagnostic.Location.SourceTree);
    }

    private static void AssertReportedAt(string source, string expectedId, string expectedLineText)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, $"ShaderDiagnosticLocation{expectedId}");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        Diagnostic diagnostic = result.Diagnostics.Single(diagnostic => diagnostic.Id == expectedId);

        Assert.AreNotEqual(Location.None, diagnostic.Location, $"{expectedId} is reported with no position at all.");

        string[] lines = source.Split('\n');
        int expectedLine = Array.FindIndex(lines, line => line.Contains(expectedLineText));

        Assert.AreNotEqual(-1, expectedLine, $"the source does not contain {expectedLineText}");
        Assert.AreEqual(expectedLine, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }
}
