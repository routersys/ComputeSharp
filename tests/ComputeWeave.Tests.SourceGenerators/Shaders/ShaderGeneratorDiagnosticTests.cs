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
    /// A thread group the hardware cannot hold, which an analyzer refuses at the attribute.
    /// </summary>
    /// <remarks>
    /// The generator carries the same bound so that the shader never reaches the compiler. Without it the
    /// author reads two refusals for one attribute, the second of them naming a line of generated code.
    /// </remarks>
    [TestMethod]
    public void AThreadGroupWithTooManyThreadsIsNotHandedToTheCompiler()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(32, 32, 2)]
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

        AssertDoesNotReport(Source, "ShaderThreadGroupTooManyThreadsCompilerTests", "CMPW0046");
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

    /// <summary>
    /// A static field initializer that reaches the field it initializes. C# runs the initializer once and
    /// reads the field as its default value where the cycle closes, but HLSL leaves the order of its global
    /// static initializers undefined, so the shader computes a value C# never produces.
    /// </summary>
    /// <remarks>
    /// The cycle closes through an imported declaration in both rows. An initializer reading a static field
    /// directly is handled by the rewriter for initializers, which rewrites constants alone, so it reaches
    /// neither the site this diagnostic is reported from nor the fault that site used to raise.
    /// </remarks>
    [TestMethod]
    [DataRow(
        "StaticFieldCycleThroughAMethodTests",
        """
        internal static class Helper
        {
            public static readonly float Value = Twice();

            public static float Twice() => Value * 2;
        }
        """)]
    [DataRow(
        "StaticFieldCycleThroughAConstructorTests",
        """
        internal static class Helper
        {
            public static readonly float Value = new Box().Amount;

            public struct Box
            {
                public float Amount;

                public Box()
                {
                    Amount = Value * 2;
                }
            }
        }
        """)]
    public void AStaticFieldInitializerReachingItselfIsDiagnosed(string assemblyName, string declarations)
    {
        AssertReportsAt(CycleShader(declarations), assemblyName, "CMPW0124", 1);
    }

    /// <summary>
    /// A cycle the initializer closes through two declarations is reported at each of the two reads. The
    /// author has to change both, and a single report on the field declaration would name neither.
    /// </summary>
    [TestMethod]
    public void AStaticFieldInitializerReachingItselfTwiceIsDiagnosedAtEachRead()
    {
        const string Declarations = """
            internal static class Helper
            {
                public static readonly float Value = Twice() + Thrice();

                public static float Twice() => Value * 2;

                public static float Thrice() => Value * 3;
            }
            """;

        AssertReportsAt(CycleShader(Declarations), "StaticFieldCycleReachedTwiceTests", "CMPW0124", 2);
    }

    /// <summary>
    /// An initializer that imports a method not reading the field back is left alone. Reporting on the
    /// import itself would refuse every initializer that calls anything.
    /// </summary>
    [TestMethod]
    public void AStaticFieldInitializerImportingAMethodIsNotDiagnosed()
    {
        const string Declarations = """
            internal static class Helper
            {
                public static readonly float Value = Twice();

                public static float Twice() => 2.0f;
            }
            """;

        AssertDoesNotReport(CycleShader(Declarations), "StaticFieldWithoutACycleTests", "CMPW0124");
    }

    /// <summary>
    /// An external static field read twice is written once and read twice. The entry the first read leaves
    /// behind is a completed one, and reading it again is not the initializer reaching the field it writes.
    /// </summary>
    /// <remarks>
    /// A shader reading such a field once never returns to a completed entry, so no row above measures which
    /// of the two kinds of entry the report is attached to. This one does, and it is the row that fails when
    /// the test telling them apart is inverted.
    /// </remarks>
    [TestMethod]
    public void AnExternalStaticFieldReadTwiceIsNotDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static readonly float Value = Twice();

                public static float Twice() => 2.0f;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[0] = Helper.Value;
                    this.buffer[1] = Helper.Value;
                }
            }
            """;

        AssertDoesNotReport(Source, "StaticFieldReadTwiceTests", "CMPW0124");
    }

    /// <summary>
    /// A static field of the shader itself whose initializer reaches the field it initializes. HLSL reads such
    /// a global as uninitialized, where C# defines the same read as the default value of the type.
    /// </summary>
    /// <remarks>
    /// A field of the shader is reached on paths of its own: neither rewriter imports the shader's own
    /// declarations, and the generator writes its static methods out before it rewrites any initializer, so
    /// there is a row per way the read can be written and per kind of declaration the initializer reaches.
    /// </remarks>
    [TestMethod]
    [DataRow(
        "ShaderStaticFieldCycleReadDirectlyTests",
        "private static readonly float Value = Value * 2;")]
    [DataRow(
        "ShaderStaticFieldCycleReadQualifiedTests",
        "private static readonly float Value = Shader.Value * 2;")]
    [DataRow(
        "ShaderStaticFieldCycleThroughItsOwnMethodTests",
        """
        private static readonly float Value = Twice();

            private static float Twice() => Value * 2;
        """)]
    [DataRow(
        "ShaderStaticFieldCycleThroughItsOwnMethodQualifiedTests",
        """
        private static readonly float Value = Twice();

            private static float Twice() => Shader.Value * 2;
        """)]
    [DataRow(
        "ShaderStaticFieldCycleThroughTwoOfItsOwnMethodsTests",
        """
        private static readonly float Value = Twice();

            private static float Twice() => Thrice() * 2;

            private static float Thrice() => Value * 3;
        """)]
    [DataRow(
        "ShaderStaticFieldCycleThroughAConstructorTests",
        """
        private static readonly float Value = new Box().Amount;

            public struct Box
            {
                public float Amount;

                public Box()
                {
                    Amount = Value * 2;
                }
            }
        """)]
    public void AStaticFieldOfTheShaderReachingItselfIsDiagnosed(string assemblyName, string members)
    {
        AssertReportsAt(ShaderWithStaticFields(members), assemblyName, "CMPW0124", 1);
    }

    /// <summary>
    /// A cycle a field of the shader closes through an imported declaration, once where that declaration reads
    /// the field back, and once where it calls a static method of the shader that does.
    /// </summary>
    /// <remarks>
    /// The read is written as a member access on the shader type, that being the only name an external
    /// declaration can reach the field by. The two rows are different reporting sites: the rewriter for a body
    /// answers the first as it rewrites the import, and the walk answers the second, the shader's own method
    /// not being imported by either rewriter.
    /// </remarks>
    [TestMethod]
    [DataRow(
        "ShaderStaticFieldCycleThroughAnImportTests",
        "public static float Go() => Shader.Value * 2;")]
    [DataRow(
        "ShaderStaticFieldCycleThroughAnImportedCallTests",
        "public static float Go() => Shader.Twice();")]
    public void AStaticFieldOfTheShaderReachedThroughAnImportIsDiagnosed(string assemblyName, string helper)
    {
        string source = $$"""
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                {{helper}}
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                internal static readonly float Value = Helper.Go();

                private readonly ReadWriteBuffer<float> buffer;

                internal static float Twice() => Value * 2;

                public void Execute()
                {
                    this.buffer[0] = Value;
                }
            }
            """;

        AssertReportsAt(source, assemblyName, "CMPW0124", 1);
    }

    /// <summary>
    /// The shapes that read a static field of the shader without the initializer reaching it back.
    /// </summary>
    /// <remarks>
    /// The first row is what separates a cycle from the ordinary shape: the same method reads the same field,
    /// and only the initializer reaching that method makes it one. All three are refused by a report that does
    /// not read the claim, and the last one by a claim that outlives the initializer it was taken for, so they
    /// are what keeps the refusal to the field being initialized while its initializer is being rewritten.
    /// </remarks>
    [TestMethod]
    [DataRow(
        "ShaderStaticFieldReadFromTheBodyTests",
        """
        private static readonly float Value = 2.0f;

            private static float Twice() => Value * 2;
        """,
        "this.buffer[0] = Twice();")]
    [DataRow(
        "ShaderStaticFieldInitializerCallingItsOwnMethodTests",
        """
        private static readonly float Value = Twice();

            private static float Twice() => 2.0f;
        """,
        "this.buffer[0] = Value;")]
    [DataRow(
        "ShaderStaticFieldReadingAnotherFieldTests",
        """
        private static readonly float Base = 2.0f;

            private static readonly float Value = Base * 2;
        """,
        "this.buffer[0] = Value;")]
    public void AStaticFieldOfTheShaderWithoutACycleIsNotDiagnosed(string assemblyName, string members, string body)
    {
        AssertDoesNotReport(ShaderWithStaticFields(members, body), assemblyName, "CMPW0124");
    }

    private static string ShaderWithStaticFields(string members, string body = "this.buffer[0] = Value;")
    {
        return $$"""
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                {{members}}

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    {{body}}
                }
            }
            """;
    }

    private static string CycleShader(string declarations)
    {
        return $$"""
            using ComputeWeave;

            namespace Shaders;

            {{declarations}}

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[0] = Helper.Value;
                }
            }
            """;
    }

    private static void AssertReportsAt(string source, string assemblyName, string expectedId, int expectedCount)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.AreEqual(
            expectedCount,
            actualIds.Count(id => id == expectedId),
            $"{expectedId} is not reported {expectedCount} time(s): {string.Join(", ", actualIds)}");
    }

    private static void AssertReports(string source, string assemblyName, string expectedId)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.IsTrue(actualIds.Contains(expectedId), $"{expectedId} is not reported: {string.Join(", ", actualIds)}");
    }

    private static void AssertDoesNotReport(string source, string assemblyName, string unexpectedId)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.IsFalse(actualIds.Contains(unexpectedId), $"{unexpectedId} is reported: {string.Join(", ", actualIds)}");
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

        // Not made distinct, so that a row can assert how many times an identifier was reported
        return [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id).Order()];
    }
}
