using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class ShaderSourceRewriterTests
{
    [TestMethod]
    public void DeclaringAnArrayWithVarIsDiagnosedWithoutFaultingTheGenerator()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    var values = new int[4];

                    this.buffer[ThreadIds.X] = values[0];
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderVarArrayTests", "CMPW0031");
    }

    [TestMethod]
    public void DeclaringALambdaIsDiagnosedWithoutFaultingTheGenerator()
    {
        const string Source = """
            using System;
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Func<int, int> identity = static value => value;

                    this.buffer[ThreadIds.X] = identity(1);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderLambdaTests", "CMPW0031");
    }

    [TestMethod]
    public void DeclaringANonStaticLocalFunctionIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private static float Helper(float value) => value * 2;

                public void Execute()
                {
                    float Helper(float value) => value * 10;

                    this.buffer[ThreadIds.X] = Helper(3);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderNonStaticLocalFunctionTests", "CMPW0113");
    }

    [TestMethod]
    public void DeclaringANonStaticLocalFunctionInAnImportedMethodIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helpers
            {
                public static float Outer(float value)
                {
                    float Inner(float inner) => inner * value;

                    return Inner(2);
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helpers.Outer(3);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderNonStaticLocalFunctionInImportTests", "CMPW0113");
    }

    private static void AssertIsDiagnosedWithoutFaulting(string source, string assemblyName, string diagnosticId)
    {
        CSharpCompilation compilation = CompilationHelper
            .CreateCompilation([source], assemblyName)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }
}
