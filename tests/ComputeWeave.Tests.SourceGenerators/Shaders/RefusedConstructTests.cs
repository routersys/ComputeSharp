using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The constructs the rewriter refuses, one row for each place that reports a refusal.
/// </summary>
/// <remarks>
/// None of these refusals had a test. What they hold in place is that a construct HLSL cannot express is
/// reported against the source the author wrote, rather than reaching the HLSL compiler and being reported
/// against generated code. A refusal that quietly stopped firing would break no build here, the constructs
/// appearing in no shader of this repository, so nothing but a test of its own can catch that.
/// </remarks>
[TestClass]
public class RefusedConstructTests
{
    [TestMethod]
    [DataRow("var value = new { First = 1 };", "ShaderAnonymousObjectTests", "CMPW0011")]
    [DataRow("int[] values = new int[] { 1, 2 };", "ShaderInitializerTests", "CMPW0059")]
    [DataRow("int[] values = [1, 2];", "ShaderCollectionExpressionTests", "CMPW0060")]
    [DataRow("int value = checked(k + 1);", "ShaderCheckedExpressionTests", "CMPW0014")]
    [DataRow("checked { k += 1; }", "ShaderCheckedStatementTests", "CMPW0015")]
    [DataRow("foreach (int value in new int[1]) { k += value; }", "ShaderForEachTests", "CMPW0017")]
    [DataRow("foreach (var (first, second) in new (int, int)[1]) { k += first + second; }", "ShaderForEachVariableTests", "CMPW0017")]
    [DataRow("object gate = new object(); lock (gate) { k += 1; }", "ShaderLockTests", "CMPW0018")]
    [DataRow("var query = from value in new int[1] select value;", "ShaderQueryTests", "CMPW0019")]
    [DataRow("System.Range range = 1..2;", "ShaderRangeTests", "CMPW0020")]
    [DataRow("if ((k, 1) is (1, 1)) { k += 1; }", "ShaderRecursivePatternTests", "CMPW0021")]
    [DataRow("ref int alias = ref k; alias += 1;", "ShaderRefTypeTests", "CMPW0022")]
    [DataRow("if (k is > 1) { k += 1; }", "ShaderRelationalPatternTests", "CMPW0023")]
    [DataRow("int size = sizeof(int);", "ShaderSizeOfTests", "CMPW0024")]
    [DataRow("System.Span<int> span = stackalloc int[2];", "ShaderStackAllocTests", "CMPW0025")]
    [DataRow("int value = k > 0 ? 1 : throw new System.Exception();", "ShaderThrowExpressionTests", "CMPW0026")]
    [DataRow("if (k < 0) { throw new System.Exception(); }", "ShaderThrowStatementTests", "CMPW0026")]
    [DataRow("try { k += 1; } catch { }", "ShaderTryTests", "CMPW0027")]
    [DataRow("(int First, int Second) pair = (1, 2); k += pair.First;", "ShaderTupleTypeTests", "CMPW0028")]
    [DataRow("using (var stream = new System.IO.MemoryStream()) { k += 1; }", "ShaderUsingStatementTests", "CMPW0029")]
    [DataRow("static System.Collections.Generic.IEnumerable<int> Values() { yield return 1; }", "ShaderYieldTests", "CMPW0030")]
    [DataRow("unsafe { k += 1; }", "ShaderUnsafeStatementTests", "CMPW0034")]
    [DataRow("using var stream = new System.IO.MemoryStream(); k += 1;", "ShaderUsingDeclarationTests", "CMPW0029")]
    [DataRow("static async System.Threading.Tasks.Task ValueAsync() { await System.Threading.Tasks.Task.Delay(0); }", "ShaderAsyncLocalFunctionTests", "CMPW0012")]
    [DataRow("static async System.Threading.Tasks.Task ValueAsync() { await System.Threading.Tasks.Task.Delay(0); }", "ShaderAwaitTests", "CMPW0013")]
    [DataRow("string text = \"a\";", "ShaderStringLiteralTests", "CMPW0036")]
    [DataRow("Shader copy = this; k += 1;", "ShaderThisExpressionTests", "CMPW0062")]
    [DataRow("float value = (float)System.Math.Abs(-1.0); k += (int)value;", "ShaderMathCallTests", "CMPW0063")]
    [DataRow("object instance = new object(); k += instance is null ? 0 : 1;", "ShaderObjectCreationTests", "CMPW0010")]
    public void ARefusedConstructIsDiagnosed(string body, string assemblyName, string expectedId)
    {
        AssertIsDiagnosed(body, assemblyName, expectedId, isUnsafe: false);
    }

    [TestMethod]
    [DataRow("int* pointer = null;", "ShaderPointerTypeTests", "CMPW0032")]
    [DataRow("delegate*<void> function = null;", "ShaderFunctionPointerTests", "CMPW0033")]
    [DataRow("int[] array = new int[1]; fixed (int* pointer = array) { k += *pointer; }", "ShaderFixedTests", "CMPW0016")]
    [DataRow("k += 1;", "ShaderUnsafeModifierTests", "CMPW0035")]
    public void ARefusedConstructNeedingAnUnsafeContextIsDiagnosed(string body, string assemblyName, string expectedId)
    {
        AssertIsDiagnosed(body, assemblyName, expectedId, isUnsafe: true);
    }

    /// <summary>
    /// A constructor of a custom type that chains to another one. This is the one refusal that needs a type
    /// of its own, so it does not fit the rows above.
    /// </summary>
    [TestMethod]
    public void AChainedConstructorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float First;

                public float Second;

                public Value(float first) : this(first, first)
                {
                }

                public Value(float first, float second)
                {
                    First = first;
                    Second = second;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = new(1.0f);

                    this.buffer[ThreadIds.X] = value.First + value.Second;
                }
            }
            """;

        AssertIsRefused(Source, "ShaderChainedConstructorTests", "CMPW0061");
    }

    /// <summary>
    /// The shader the report was written around, kept as it was written.
    /// </summary>
    /// <remarks>
    /// The construct is one the rows above already cover. What this pins is the shape the report carried: the
    /// captured resource arrives through a primary constructor rather than a field, which is a different path
    /// through the generator, and it is the source a reader will reach for when checking the behavior.
    /// </remarks>
    [TestMethod]
    public void TheShaderTheReportWasWrittenAroundIsRefused()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Case(ReadWriteBuffer<float> buffer) : IComputeShader
            {
                public void Execute()
                {
                    int[] values = [1, 2, 3];

                    buffer[ThreadIds.X] = values[0];
                }
            }
            """;

        AssertIsRefused(Source, "ShaderReportedShapeTests", "CMPW0060");
    }

    /// <summary>
    /// A shader the rewriter accepts and the HLSL compiler refuses.
    /// </summary>
    /// <remarks>
    /// Recursion is what the HLSL compiler refuses, and the rewriter has nothing to say about the local
    /// function carrying it, so a refusal is the only thing that keeps a shader from reaching the compiler.
    /// Without this row, removing the forwarding outright would leave every row above passing.
    /// </remarks>
    [TestMethod]
    public void AnInputTheRewriterAcceptsCarriesTheCompilerFailure()
    {
        Diagnostic[] reported = Report(
            Shader("static int Fib(int n) => n <= 1 ? n : Fib(n - 1) + Fib(n - 2); k += Fib(3);", isUnsafe: false),
            "ShaderAcceptedCompilerFailureTests");

        Assert.AreEqual("CMPW0046", Ids(reported));
    }

    /// <summary>
    /// A shader carrying syntax the accepted set does not cover.
    /// </summary>
    /// <remarks>
    /// The report refuses the input, so the shader never reaches the HLSL compiler. The body carries recursion,
    /// which HLSL cannot express under any profile, so the compiler would answer for it were it handed the
    /// shader: what the row reads is the refusal arriving alone, and not a body the compiler happens to accept.
    /// </remarks>
    [TestMethod]
    public void AReportForSyntaxWithNoVerdictRefusesTheInput()
    {
        Diagnostic[] reported = Report(
            Shader(
                """
                float v = 5;

                goto done;

                done: v += 1;

                static int Fib(int n) => n <= 1 ? n : Fib(n - 1) + Fib(n - 2);

                k += Fib(3) + (int)v;
                """,
                isUnsafe: false),
            "ShaderReportedRefusalTests");

        Assert.AreEqual("CMPW0121", Ids(reported));
    }

    /// <summary>
    /// A refused construct that carries syntax the set has no verdict for, both under it and beside it.
    /// </summary>
    /// <remarks>
    /// The refusal names the place the author has to change, so the record is dropped rather than naming the
    /// array syntax the refused statement holds as well. Reading the whole set is what makes one cause name
    /// one place, the record and the refusal being produced by different parts of the same walk.
    /// </remarks>
    [TestMethod]
    public void ARefusedConstructIsNotRecordedAsSyntaxWithNoVerdict()
    {
        Diagnostic[] reported = Report(
            Shader("foreach (int value in new int[1]) { k += value; }", isUnsafe: false),
            "ShaderRefusalWithoutRecordTests");

        Assert.AreEqual("CMPW0017", Ids(reported));
    }

    /// <summary>
    /// Builds a shader around a body, runs the generator over it, and asserts the refusal is reported.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <param name="expectedId">The identifier the refusal is expected to carry.</param>
    /// <param name="isUnsafe">Whether the entry point needs an unsafe context.</param>
    private static void AssertIsDiagnosed(string body, string assemblyName, string expectedId, bool isUnsafe)
    {
        AssertIsRefused(Shader(body, isUnsafe), assemblyName, expectedId);
    }

    /// <summary>
    /// Runs the generator over a shader and asserts the refusal is reported and the HLSL compiler is not reached.
    /// </summary>
    /// <param name="source">The source of the shader to run the generator over.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <param name="expectedId">The identifier the refusal is expected to carry.</param>
    private static void AssertIsRefused(string source, string assemblyName, string expectedId)
    {
        Diagnostic[] reported = Report(source, assemblyName);

        // The refusal is asserted by identifier and not as a set, one body being able to trip several of them
        Assert.IsTrue(reported.Any(diagnostic => diagnostic.Id == expectedId), Ids(reported));

        // Nothing arrives from the HLSL compiler, which would name generated code the author never wrote
        Assert.IsFalse(reported.Any(static diagnostic => diagnostic.Id == "CMPW0046"), Ids(reported));
    }

    /// <summary>
    /// Runs the generator over a shader and gets the diagnostics it reports.
    /// </summary>
    /// <param name="source">The source of the shader to run the generator over.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The diagnostics the generator reported for <paramref name="source"/>.</returns>
    private static Diagnostic[] Report(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([source], assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return [.. result.Diagnostics];
    }

    /// <summary>
    /// Builds a shader around a body.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="isUnsafe">Whether the entry point needs an unsafe context.</param>
    /// <returns>The source of a shader carrying <paramref name="body"/>.</returns>
    private static string Shader(string body, bool isUnsafe)
    {
        return $$"""
            using System.Linq;
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public {{(isUnsafe ? "unsafe " : "")}}void Execute()
                {
                    int k = 1;

                    {{body}}

                    this.buffer[ThreadIds.X] = k;
                }
            }
            """;
    }

    /// <summary>
    /// Joins the identifiers of the reported diagnostics, for the message an assertion fails with.
    /// </summary>
    /// <param name="reported">The diagnostics the generator reported.</param>
    /// <returns>The distinct identifiers of <paramref name="reported"/>, in order.</returns>
    private static string Ids(Diagnostic[] reported)
    {
        return string.Join(", ", reported.Select(static diagnostic => diagnostic.Id).Distinct().OrderBy(static id => id));
    }
}
