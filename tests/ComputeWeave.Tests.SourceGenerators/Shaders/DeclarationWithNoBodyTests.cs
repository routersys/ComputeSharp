using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// What a declaration carrying no body is answered with. What reaches the generated HLSL is built from the
/// body, so a declaration that has none has nothing to write, and C# allows one: it reports an extern
/// declaration as a warning.
/// </summary>
/// <remarks>
/// The rows are the routes a declaration reaches the rewriting by rather than the shapes an extern
/// declaration can take, the rewriting being what has to answer. Every route lands on the visit that
/// normalizes the body of one of the three kinds a declaration can be, so those three cover the four imports
/// and the entry point of each of the two generators.
/// </remarks>
[TestClass]
public class DeclarationWithNoBodyTests
{
    [TestMethod]
    [DataRow(
        "BodilessStaticMethodFromBodyTests",
        """
        internal static class Helper
        {
            public static extern float Twice(float value);
        }
        """,
        "",
        "this.buffer[0] = Helper.Twice(2.0f);")]
    [DataRow(
        "BodilessInstanceMethodFromBodyTests",
        """
        internal struct Helper
        {
            public float Amount;

            public extern float Doubled();
        }
        """,
        "",
        """
        Helper helper = default;

                    this.buffer[0] = helper.Doubled();
        """)]
    [DataRow(
        "BodilessStaticMethodFromInitializerTests",
        """
        internal static class Helper
        {
            public static extern float Twice(float value);
        }
        """,
        "private static readonly float Scale = Helper.Twice(2.0f);",
        "this.buffer[0] = Scale;")]
    [DataRow(
        "BodilessConstructorFromBodyTests",
        """
        internal struct Helper
        {
            public float Amount;

            public extern Helper(float amount);
        }
        """,
        "",
        "this.buffer[0] = new Helper(2.0f).Amount;")]
    [DataRow(
        "BodilessConstructorFromInitializerTests",
        """
        internal struct Helper
        {
            public float Amount;

            public extern Helper(float amount);

            public static float Read(Helper helper) => helper.Amount;
        }
        """,
        "private static readonly float Scale = Helper.Read(new Helper(2.0f));",
        "this.buffer[0] = Scale;")]
    [DataRow(
        "BodilessMethodOfTheShaderTests",
        "",
        "private extern float Twice(float value);",
        "this.buffer[0] = Twice(2.0f);")]
    [DataRow(
        "BodilessLocalFunctionInTheBodyTests",
        "",
        "",
        """
        static extern float Twice(float value);

                    this.buffer[0] = Twice(2.0f);
        """)]
    [DataRow(
        "BodilessLocalFunctionInAnImportTests",
        """
        internal static class Helper
        {
            public static float Outer(float value)
            {
                static extern float Twice(float inner);

                return Twice(value);
            }
        }
        """,
        "",
        "this.buffer[0] = Helper.Outer(2.0f);")]
    public void ADeclarationWithNoBodyIsDiagnosed(string assemblyName, string declarations, string members, string body)
    {
        AssertReportsOnly(Shader(declarations, members, body), assemblyName, "CMPW0127");
    }

    /// <summary>
    /// The entry point written with no body, which is the one route that imports no declaration.
    /// </summary>
    [TestMethod]
    public void AnEntryPointWithNoBodyIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public extern void Execute();
            }
            """;

        AssertReportsOnly(source, "BodilessEntryPointTests", "CMPW0127");
    }

    /// <summary>
    /// The same three kinds written with a body, so that the rows above answer for the body being absent
    /// rather than for the kind. Each is required to produce source as well, an identifier being absent from
    /// a run that generated nothing saying nothing.
    /// </summary>
    [TestMethod]
    [DataRow(
        "MethodWithABodyTests",
        """
        internal static class Helper
        {
            public static float Twice(float value) => value * 2;
        }
        """,
        "",
        "this.buffer[0] = Helper.Twice(2.0f);")]
    [DataRow(
        "ConstructorWithABodyTests",
        """
        internal struct Helper
        {
            public float Amount;

            public Helper(float amount)
            {
                Amount = amount;
            }
        }
        """,
        "",
        "this.buffer[0] = new Helper(2.0f).Amount;")]
    [DataRow(
        "LocalFunctionWithABodyTests",
        "",
        "",
        """
        static float Twice(float value) => value * 2;

                    this.buffer[0] = Twice(2.0f);
        """)]
    public void ADeclarationWithABodyIsNotDiagnosed(string assemblyName, string declarations, string members, string body)
    {
        GeneratorRunResult result = Run(Shader(declarations, members, body), assemblyName);

        Assert.IsTrue(result.Diagnostics.IsEmpty, string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
        Assert.AreNotEqual(0, GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader").Length);
    }

    private static string Shader(string declarations, string members, string body)
    {
        return $$"""
            using ComputeWeave;

            namespace Shaders;

            {{declarations}}

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

    private static void AssertReportsOnly(string source, string assemblyName, string expectedId)
    {
        ImmutableArray<Diagnostic> diagnostics = Run(source, assemblyName).Diagnostics;
        string[] actualIds = [.. diagnostics.Select(static diagnostic => diagnostic.Id).Distinct().Order()];

        Assert.IsTrue(actualIds.SequenceEqual([expectedId]), $"{expectedId} is not the only report: {string.Join(", ", actualIds)}");
    }

    private static GeneratorRunResult Run(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        // A fault discards the output for the whole compilation unit, so it is read ahead of the reports
        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return result;
    }
}
