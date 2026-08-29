using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// What a static field initializer may call. A different rewriter handles those initializers from the one
/// that handles the shader body, so the two can disagree about the same call.
/// </summary>
/// <remarks>
/// The shaders here carry a thread group size, which is what turns shader compilation on, so a generated
/// source that HLSL rejects reaches these tests as a diagnostic rather than as a passing string match.
/// </remarks>
[TestClass]
public class StaticFieldInitializerTests
{
    private const string IntrinsicSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Hlsl.Abs(-2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    private const string ShaderMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Member(2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            private static float Member(float value) => value * 2;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    [TestMethod]
    public void AnIntrinsicIsWrittenUnderItsHlslName()
    {
        string generated = Generate(IntrinsicSource, "StaticFieldIntrinsicTests");

        Assert.IsTrue(generated.Contains("abs(-2.0)"), generated);
    }

    /// <summary>
    /// A call to a function the generator wrote is accepted in a static field initializer, because the
    /// forward declarations are written ahead of the static fields. This is what makes it possible to
    /// import a method into an initializer at all, so it is pinned rather than left to be re-derived.
    /// </summary>
    [TestMethod]
    public void AMethodOfTheShaderTypeIsCalled()
    {
        string generated = Generate(ShaderMethodSource, "StaticFieldShaderMethodTests");

        Assert.IsTrue(generated.Contains("Member(2.0)"), generated);
    }

    private static string Generate(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        ImmutableArray<Diagnostic> diagnostics = result.Diagnostics;

        Assert.IsTrue(
            diagnostics.IsEmpty,
            string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader");
    }
}
