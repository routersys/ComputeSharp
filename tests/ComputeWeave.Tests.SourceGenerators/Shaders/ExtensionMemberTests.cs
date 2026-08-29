using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The C# 14 extension block, which declares its members on a type of its own. A member declared there is
/// not imported, whereas a static method declared alongside it belongs to the enclosing class and is.
/// </summary>
[TestClass]
public class ExtensionMemberTests
{
    /// <summary>
    /// The reproduction from the issue this diagnostic was added for.
    /// </summary>
    private const string InstanceMemberSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Ext
        {
            extension(float value)
            {
                public float Doubled() => value * 2;
            }
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = 2.0f.Doubled();
            }
        }
        """;

    /// <summary>
    /// The control. A static method written inside an extension block is declared on the enclosing static
    /// class, so it reaches the same import path as any other static method and has to keep working.
    /// </summary>
    private const string StaticMemberSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Ext
        {
            extension(float value)
            {
                public static float Origin() => 0;
            }
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Ext.Origin();
            }
        }
        """;

    [TestMethod]
    public void CallingAnExtensionMemberIsDiagnosed()
    {
        AssertIsDiagnosed(InstanceMemberSource, "ShaderExtensionMemberTests", "CMPW0119");
    }

    [TestMethod]
    public void CallingAStaticMemberOfAnExtensionBlockIsImported()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [StaticMemberSource],
            "ShaderExtensionStaticMemberTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());

        string generated = GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Shaders.Shader");

        Assert.IsTrue(generated.Contains("Shaders_Ext_Origin()"), $"the static member is not imported:\n{generated}");
    }

    private static void AssertIsDiagnosed(string source, string assemblyName, string diagnosticId)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }
}
