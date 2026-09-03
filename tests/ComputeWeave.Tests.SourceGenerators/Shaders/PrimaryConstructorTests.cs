using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Which constructor a shader may call, and which diagnostic names the reason. A primary constructor and a
/// constructor from another assembly both fail to resolve to a constructor declaration, and used to be
/// reported alike, although only one of the two is missing its source.
/// </summary>
[TestClass]
public class PrimaryConstructorTests
{
    /// <summary>
    /// The reproduction from the issue this diagnostic was added for.
    /// </summary>
    private const string PrimaryConstructorSource = """
        using ComputeWeave;

        namespace Shaders;

        internal readonly struct Helper(float value)
        {
            public readonly float Doubled() => value * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                Helper helper = new(2.0f);

                this.buffer[0] = helper.Doubled();
            }
        }
        """;

    /// <summary>
    /// The same type written with an explicit constructor, which is what the diagnostic asks for.
    /// </summary>
    private const string ExplicitConstructorSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Value;

            public Helper(float value)
            {
                Value = value;
            }

            public readonly float Doubled() => Value * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                Helper helper = new(2.0f);

                this.buffer[0] = helper.Doubled();
            }
        }
        """;

    /// <summary>
    /// A constructor with no source at all, which keeps the diagnostic that names that as the reason.
    /// </summary>
    private const string ExternalConstructorSource = """
        using System.Numerics;
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                Vector2 vector = new(1.0f, 2.0f);

                this.buffer[0] = vector.X;
            }
        }
        """;

    /// <summary>
    /// The shader's own primary constructor, whose captures become the shader fields. This is the asymmetry
    /// the previous wording hid: it is only the constructed auxiliary type that is refused.
    /// </summary>
    private const string ShaderPrimaryConstructorSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader(ReadWriteBuffer<float> buffer) : IComputeShader
        {
            public void Execute()
            {
                buffer[0] = 2.0f;
            }
        }
        """;

    /// <summary>
    /// The same type constructed in a static field initializer. That path answered a constructor with a
    /// default value and reported nothing, so the refusal reaches it only once the import does.
    /// </summary>
    private const string PrimaryConstructorInitializerSource = """
        using ComputeWeave;

        namespace Shaders;

        internal readonly struct Helper(float value)
        {
            public readonly float Doubled() => value * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = new Helper(2.0f).Doubled();

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    [TestMethod]
    public void ConstructingATypeWithAPrimaryConstructorIsDiagnosed()
    {
        AssertReports(PrimaryConstructorSource, "PrimaryConstructorTests", "CMPW0120", "CMPW0049");
    }

    /// <summary>
    /// A type from another assembly is refused for the reason the older diagnostic states, so it keeps it.
    /// It draws further diagnostics of its own, which is why only the two that tell the cases apart are read.
    /// </summary>
    [TestMethod]
    public void ConstructingATypeFromAnotherAssemblyKeepsItsOwnDiagnostic()
    {
        AssertReports(ExternalConstructorSource, "ExternalConstructorTests", "CMPW0049", "CMPW0120");
    }

    [TestMethod]
    public void ConstructingATypeWithAnExplicitConstructorIsAccepted()
    {
        AssertNoDiagnostics(ExplicitConstructorSource, "ExplicitConstructorTests");
    }

    [TestMethod]
    public void TheShaderOwnPrimaryConstructorIsAccepted()
    {
        AssertNoDiagnostics(ShaderPrimaryConstructorSource, "ShaderPrimaryConstructorTests");
    }

    /// <summary>
    /// A static field initializer refuses the same construction the shader body refuses, rather than
    /// answering with a default value and letting the shader compute a different number in silence.
    /// </summary>
    [TestMethod]
    public void ConstructingATypeWithAPrimaryConstructorInAStaticFieldInitializerIsDiagnosed()
    {
        AssertReports(PrimaryConstructorInitializerSource, "PrimaryConstructorInitializerTests", "CMPW0120", "CMPW0049");
    }

    private static void AssertReports(string source, string assemblyName, string expectedId, string unexpectedId)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.IsTrue(actualIds.Contains(expectedId), $"{expectedId} is not reported: {string.Join(", ", actualIds)}");
        Assert.IsFalse(actualIds.Contains(unexpectedId), $"{unexpectedId} is reported as well: {string.Join(", ", actualIds)}");
    }

    private static void AssertNoDiagnostics(string source, string assemblyName)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.AreEqual(0, actualIds.Length, string.Join(", ", actualIds));
    }

    private static string[] Run(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id).Distinct().Order()];
    }
}
