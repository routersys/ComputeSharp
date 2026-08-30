using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The requirement that unsafe blocks be enabled, which the generator needs to emit valid code.
/// </summary>
/// <remarks>
/// Without the option the generator returns before it reaches a shader body, producing neither source nor
/// diagnostics of its own, so this analyzer is the only thing that tells the author why nothing happened.
/// The Direct2D counterpart has had this pair since it was written; this is the compute half.
/// </remarks>
[TestClass]
public class AllowUnsafeBlocksTests
{
    private const string Source = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 1;
            }
        }
        """;

    [TestMethod]
    public void AShaderCompiledWithoutTheOptionIsDiagnosed()
    {
        AnalyzerHelper.AssertDiagnostics(
            new MissingAllowUnsafeBlocksCompilationOptionAnalyzer(),
            [Source],
            "ShaderMissingAllowUnsafeBlocksTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            "CMPW0052");
    }

    [TestMethod]
    public void AShaderCompiledWithTheOptionIsNotDiagnosed()
    {
        AnalyzerHelper.AssertDiagnostics(
            new MissingAllowUnsafeBlocksCompilationOptionAnalyzer(),
            [Source],
            "ShaderWithAllowUnsafeBlocksTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    [TestMethod]
    public void TheCompilationTheHelperMakesReachesTheShaderBody()
    {
        // A helper that left the option off would run the generator to an empty result, and a test asserting
        // the absence of something would then pass without the generator having looked at the shader at all
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([Source], "ShaderDefaultOptionsTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.GeneratedSources.Any(static source => source.HintName.Contains("Shaders.Shader")),
            string.Join(", ", result.GeneratedSources.Select(static source => source.HintName)));
    }
}
