using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The refusals the shader descriptor generator reports itself, rather than through an analyzer.
/// </summary>
/// <remarks>
/// <para>
/// There is one row per reporting site. Five of these identifiers are reported from two places, the rewriter
/// that walks the shader body and the one that walks a static field initializer, so a row that covers one of
/// the two would leave the other free to stop working.
/// </para>
/// <para>
/// Only the identifier is asserted. Pinning the location or the message would make every row fail on a change
/// to the wording, and what these rows exist to catch is a refusal that stops happening at all.
/// </para>
/// </remarks>
[TestClass]
public class ShaderGeneratorDiagnosticTests
{
    [TestMethod]
    [DataRow("private readonly float[] values;", "this.buffer[0] = 1;", "ShaderArrayFieldTests", "CMPW0001")]
    [DataRow("private readonly string text;", "this.buffer[0] = 1;", "ShaderManagedFieldTests", "CMPW0001")]
    [DataRow("private float Value() => ThreadIds.X;", "this.buffer[0] = Value();", "ShaderThreadIdsInAMethodTests", "CMPW0006")]
    [DataRow("private float Value() => GroupIds.X;", "this.buffer[0] = Value();", "ShaderGroupIdsInAMethodTests", "CMPW0007")]
    [DataRow("private float Value() => GroupSize.X;", "this.buffer[0] = Value();", "ShaderGroupSizeInAMethodTests", "CMPW0008")]
    [DataRow("private float Value() => GridIds.X;", "this.buffer[0] = Value();", "ShaderGridIdsInAMethodTests", "CMPW0009")]
    [DataRow("private float Value() => DispatchSize.X;", "this.buffer[0] = Value();", "ShaderDispatchSizeInAMethodTests", "CMPW0039")]
    [DataRow("private static readonly float Value = ThreadIds.X;", "this.buffer[0] = Value;", "ShaderThreadIdsInAStaticFieldTests", "CMPW0006")]
    [DataRow("private static readonly float Value = GroupIds.X;", "this.buffer[0] = Value;", "ShaderGroupIdsInAStaticFieldTests", "CMPW0007")]
    [DataRow("private static readonly float Value = GroupSize.X;", "this.buffer[0] = Value;", "ShaderGroupSizeInAStaticFieldTests", "CMPW0008")]
    [DataRow("private static readonly float Value = GridIds.X;", "this.buffer[0] = Value;", "ShaderGridIdsInAStaticFieldTests", "CMPW0009")]
    [DataRow("private static readonly float Value = DispatchSize.X;", "this.buffer[0] = Value;", "ShaderDispatchSizeInAStaticFieldTests", "CMPW0039")]
    [DataRow("private static readonly System.DateTime Value;", "this.buffer[0] = 1;", "ShaderStaticFieldTypeTests", "CMPW0038")]
    [DataRow("public float Value => 1;", "this.buffer[0] = 1;", "ShaderPropertyTests", "CMPW0040")]
    public void AShaderMemberTheGeneratorRefusesIsDiagnosed(string member, string body, string assemblyName, string expectedId)
    {
        AssertReports(Shader(member, body), assemblyName, expectedId);
    }

    /// <summary>
    /// A compute shader with no resource of its own. The generated entry point would bind nothing.
    /// </summary>
    [TestMethod]
    public void AShaderWithoutAResourceIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly float value;

                public void Execute()
                {
                }
            }
            """;

        AssertReports(Source, "ShaderWithoutResourceTests", "CMPW0005");
    }

    /// <summary>
    /// The second of the two places that report an invalid property, the field a property causes to exist.
    /// </summary>
    /// <remarks>
    /// The property itself is an explicit interface implementation, which the first place skips, so this
    /// row fails only if the second place stops reporting.
    /// </remarks>
    [TestMethod]
    public void AFieldGeneratedForAPropertyIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal interface INamed
            {
                int Id { get; }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader, INamed
            {
                private readonly ReadWriteBuffer<float> buffer;

                int INamed.Id { get; }

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = 1;
                }
            }
            """;

        AssertReports(Source, "ShaderGeneratedPropertyFieldTests", "CMPW0040");
    }

    /// <summary>
    /// A thread group size the analyzer accepts and the shader compiler does not.
    /// </summary>
    /// <remarks>
    /// Each of the three values is inside its own range, so the refusal comes from the product exceeding
    /// what a group may hold. Reaching the compiler at all is the point: this is the identifier that carries
    /// a compiler failure back to the author.
    /// </remarks>
    [TestMethod]
    public void AShaderTheCompilerRefusesIsDiagnosed()
    {
        const string Source = """
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

        AssertReports(Source, "ShaderCompilerFailureTests", "CMPW0046");
    }

    /// <summary>
    /// A shader that operates on a value of double precision without declaring that it needs the support.
    /// </summary>
    /// <remarks>
    /// The width has to come from the type of a captured value. An unsuffixed literal is single precision to
    /// the compiler this generator uses, so writing one would leave the shader asking for nothing.
    /// </remarks>
    [TestMethod]
    public void AShaderNeedingDoublePrecisionWithoutTheAttributeIsDiagnosed()
    {
        AssertReports(DoublePrecisionShader("", "double"), "ShaderMissingDoublePrecisionTests", "CMPW0064");
    }

    [TestMethod]
    public void AShaderWithTheAttributeAndNoDoublePrecisionIsDiagnosed()
    {
        AssertReports(
            DoublePrecisionShader("[RequiresDoublePrecisionSupport]", "float"),
            "ShaderUnnecessaryDoublePrecisionTests",
            "CMPW0065");
    }

    /// <summary>
    /// The control. A shader that uses none of the forms above has to leave the generator silent.
    /// </summary>
    [TestMethod]
    public void AValidShaderIsNotDiagnosed()
    {
        string[] actualIds = Run(Shader("private readonly float scale;", "this.buffer[0] = this.scale;"), "ShaderValidTests");

        Assert.AreEqual(0, actualIds.Length, string.Join(", ", actualIds));
    }

    private static string Shader(string member, string body)
    {
        return $$"""
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                {{member}}

                public void Execute()
                {
                    {{body}}
                }
            }
            """;
    }

    private static string DoublePrecisionShader(string attribute, string fieldType)
    {
        return $$"""
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            {{attribute}}
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;
                private readonly {{fieldType}} factor;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = (float)(this.buffer[ThreadIds.X] * this.factor);
                }
            }
            """;
    }

    private static void AssertReports(string source, string assemblyName, string expectedId)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.IsTrue(actualIds.Contains(expectedId), $"{expectedId} is not reported: {string.Join(", ", actualIds)}");
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
