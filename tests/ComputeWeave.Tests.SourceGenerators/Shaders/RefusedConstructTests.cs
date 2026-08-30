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

        CSharpCompilation compilation = CompilationHelper.CreateCompilation([Source], "ShaderChainedConstructorTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.Any(static diagnostic => diagnostic.Id == "CMPW0061"),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
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
        string source = $$"""
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

        CSharpCompilation compilation = CompilationHelper.CreateCompilation([source], assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        // The refusal is asserted by identifier and not as a set: a refused input is still handed to the HLSL
        // compiler, and what comes back from there is the subject of a separate change
        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == expectedId),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }
}
