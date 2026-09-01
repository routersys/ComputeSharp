using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class GroupSharedFieldTests
{
    private const string PointerElementSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly unsafe partial struct Shader : IComputeShader
        {
            [GroupShared]
            private static readonly int*[] pointers = null!;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 0;
            }
        }
        """;

    private const string NotAnArraySource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            [GroupShared]
            private static readonly float scalar;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 0;
            }
        }
        """;

    private const string ManagedElementSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            [GroupShared]
            private static readonly string[] managed = null!;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 0;
            }
        }
        """;

    [TestMethod]
    public void AnArrayOfPointersDoesNotFaultTheGenerator()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [PointerElementSource],
            "GroupSharedPointerElementTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
    }

    [TestMethod]
    public void AnArrayOfPointersIsDiagnosed()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGroupSharedFieldDeclarationAnalyzer(),
            [PointerElementSource],
            "GroupSharedPointerElementAnalyzerTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true),
            "CMPW0004");
    }

    [TestMethod]
    public void AFieldThatIsNotAnArrayIsDiagnosed()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGroupSharedFieldDeclarationAnalyzer(),
            [NotAnArraySource],
            "GroupSharedNotAnArrayAnalyzerTests",
            "CMPW0004");
    }

    [TestMethod]
    public void AnArrayOfAManagedTypeIsDiagnosed()
    {
        AnalyzerHelper.AssertDiagnostics(
            new InvalidGroupSharedFieldDeclarationAnalyzer(),
            [ManagedElementSource],
            "GroupSharedManagedElementAnalyzerTests",
            "CMPW0004");
    }

    private const string DefaultSizedArraySource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            [GroupShared]
            private static readonly int[] cache;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = cache[0];
            }
        }
        """;

    /// <summary>
    /// A field with no explicit size, whose HLSL declaration falls back to the group's full volume.
    /// </summary>
    /// <remarks>
    /// The fallback expression multiplies all three axes together. A shader with a one-dimensional thread
    /// group cannot tell a missing axis from one whose size is 1, so this checks the written expression
    /// itself rather than a shader that only varies one axis.
    /// </remarks>
    [TestMethod]
    public void ADefaultSizedArrayUsesTheFullGroupVolume()
    {
        string generated = Generate(DefaultSizedArraySource, "GroupSharedDefaultSizeTests");

        Assert.IsTrue(
            generated.Contains("groupshared int cache [__GroupSize__get_X * __GroupSize__get_Y * __GroupSize__get_Z];"),
            generated);
    }

    private static string Generate(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([source], assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.IsEmpty,
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader");
    }
}
