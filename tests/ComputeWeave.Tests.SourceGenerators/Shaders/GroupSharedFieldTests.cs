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
}
