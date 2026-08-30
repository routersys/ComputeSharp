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
}
