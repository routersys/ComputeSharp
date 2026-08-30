using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// The constructs the rewriter refuses when it runs for a pixel shader, one row for each place that reports
/// a refusal.
/// </summary>
/// <remarks>
/// <para>
/// The rewriter is shared with the compute generator and the reporting sites are the same ones. What differs
/// is the identifier each site carries, which the pixel shader side supplies through its own descriptors, so
/// a row here fails when that side stops answering even though the compute row still passes.
/// </para>
/// <para>
/// None of these refusals had a test. The constructs appear in no shader of this repository, so a refusal
/// that stopped firing would break no build and no other test.
/// </para>
/// </remarks>
[TestClass]
public class Test_D2DPixelShaderDescriptorGenerator_RefusedConstructs
{
    [TestMethod]
    [DataRow("object instance = new object(); k += instance is null ? 0 : 1;", "CMPWD2D0002")]
    [DataRow("var value = new { First = 1 };", "CMPWD2D0003")]
    [DataRow("int value = checked(k + 1);", "CMPWD2D0006")]
    [DataRow("checked { k += 1; }", "CMPWD2D0007")]
    [DataRow("foreach (int value in new int[1]) { k += value; }", "CMPWD2D0009")]
    [DataRow("foreach (var (first, second) in new (int, int)[1]) { k += first + second; }", "CMPWD2D0009")]
    [DataRow("object gate = new object(); lock (gate) { k += 1; }", "CMPWD2D0010")]
    [DataRow("var query = from value in new int[1] select value;", "CMPWD2D0011")]
    [DataRow("System.Range range = 1..2;", "CMPWD2D0012")]
    [DataRow("if ((k, 1) is (1, 1)) { k += 1; }", "CMPWD2D0013")]
    [DataRow("ref int alias = ref k; alias += 1;", "CMPWD2D0014")]
    [DataRow("if (k is > 1) { k += 1; }", "CMPWD2D0015")]
    [DataRow("int size = sizeof(int);", "CMPWD2D0016")]
    [DataRow("System.Span<int> span = stackalloc int[2];", "CMPWD2D0017")]
    [DataRow("int value = k > 0 ? 1 : throw new System.Exception();", "CMPWD2D0018")]
    [DataRow("if (k < 0) { throw new System.Exception(); }", "CMPWD2D0018")]
    [DataRow("try { k += 1; } catch { }", "CMPWD2D0019")]
    [DataRow("(int First, int Second) pair = (1, 2); k += pair.First;", "CMPWD2D0020")]
    [DataRow("using (var stream = new System.IO.MemoryStream()) { k += 1; }", "CMPWD2D0021")]
    [DataRow("using var stream = new System.IO.MemoryStream(); k += 1;", "CMPWD2D0021")]
    [DataRow("static System.Collections.Generic.IEnumerable<int> Values() { yield return 1; }", "CMPWD2D0022")]
    [DataRow("var values = new int[1]; k += values[0];", "CMPWD2D0023")]
    [DataRow("string text = \"a\";", "CMPWD2D0028")]
    [DataRow("static async System.Threading.Tasks.Task ValueAsync() { await System.Threading.Tasks.Task.Delay(0); }", "CMPWD2D0004")]
    [DataRow("static async System.Threading.Tasks.Task ValueAsync() { await System.Threading.Tasks.Task.Delay(0); }", "CMPWD2D0005")]
    [DataRow("int value = System.Convert.ToInt32(1.0f); k += value;", "CMPWD2D0040")]
    [DataRow("int[] values = new int[] { 1, 2 };", "CMPWD2D0071")]
    [DataRow("int[] values = [1, 2];", "CMPWD2D0072")]
    [DataRow("Shader copy = this; k += 1;", "CMPWD2D0074")]
    [DataRow("float value = (float)System.Math.Abs(-1.0); k += (int)value;", "CMPWD2D0075")]
    [DataRow("float Helper(float value) => value * 10; k += (int)Helper(3);", "CMPWD2D0087")]
    public void ARefusedConstructIsDiagnosed(string body, string expectedId)
    {
        AssertIsDiagnosed(body, expectedId, isUnsafe: false);
    }

    [TestMethod]
    [DataRow("int[] array = new int[1]; fixed (int* pointer = array) { k += *pointer; }", "CMPWD2D0008")]
    [DataRow("int* pointer = null;", "CMPWD2D0024")]
    [DataRow("delegate*<void> function = null;", "CMPWD2D0025")]
    [DataRow("unsafe { k += 1; }", "CMPWD2D0026")]
    [DataRow("k += 1;", "CMPWD2D0027")]
    public void ARefusedConstructNeedingAnUnsafeContextIsDiagnosed(string body, string expectedId)
    {
        AssertIsDiagnosed(body, expectedId, isUnsafe: true);
    }

    /// <summary>
    /// The control. The shader the rows above are built around has to leave the generator silent on its own,
    /// so that every row above is answered by the body it carries and not by the shape holding it.
    /// </summary>
    [TestMethod]
    public void TheShaderTheRowsAreBuiltAroundIsNotDiagnosed()
    {
        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(Shader("k += 1;", isUnsafe: false));
    }

    /// <summary>
    /// A swizzled matrix indexer whose arguments are not constants.
    /// </summary>
    [TestMethod]
    public void IndexingAMatrixWithANonConstantSwizzleIsDiagnosed()
    {
        AssertIsDiagnosed(
            """
            Float2x2 matrix = new(1, 2, 3, 4);
            MatrixIndex row = MatrixIndex.M11;

            k += (int)matrix[row, MatrixIndex.M12].X;
            """,
            "CMPWD2D0029",
            isUnsafe: false);
    }

    /// <summary>
    /// A constructor of a custom type that chains to another one, which needs a type of its own.
    /// </summary>
    [TestMethod]
    public void AChainedConstructorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;

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

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct Shader : ID2D1PixelShader
            {
                public Float4 Execute()
                {
                    Value value = new(1.0f);

                    return value.First + value.Second;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(Source, "CMPWD2D0073");
    }

    [TestMethod]
    [DataRow("private readonly float[] values;", "CMPWD2D0001")]
    [DataRow("private readonly string text;", "CMPWD2D0001")]
    [DataRow("private static readonly System.DateTime Value;", "CMPWD2D0030")]
    [DataRow("public float Value => 1;", "CMPWD2D0031")]
    public void AShaderMemberTheGeneratorRefusesIsDiagnosed(string member, string expectedId)
    {
        const string Template = """
            using ComputeWeave;
            using ComputeWeave.D2D1;

            namespace Shaders;

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct Shader : ID2D1PixelShader
            {
                MEMBER

                public Float4 Execute()
                {
                    return 1;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(
            Template.Replace("MEMBER", member),
            expectedId);
    }

    /// <summary>
    /// The second of the two places that report an invalid property, the field a property causes to exist.
    /// </summary>
    /// <remarks>
    /// The property itself is an explicit interface implementation, which the first place skips, so this row
    /// fails only if the second place stops reporting.
    /// </remarks>
    [TestMethod]
    public void AFieldGeneratedForAPropertyIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;

            namespace Shaders;

            internal interface INamed
            {
                int Id { get; }
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct Shader : ID2D1PixelShader, INamed
            {
                int INamed.Id { get; }

                public Float4 Execute()
                {
                    return 1;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(Source, "CMPWD2D0031");
    }

    /// <summary>
    /// Builds a pixel shader around a body, runs the generator over it, and asserts the refusal is reported.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="expectedId">The identifier the refusal is expected to carry.</param>
    /// <param name="isUnsafe">Whether the entry point needs an unsafe context.</param>
    private static void AssertIsDiagnosed(string body, string expectedId, bool isUnsafe)
    {
        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(Shader(body, isUnsafe), expectedId);
    }

    /// <summary>
    /// Builds a pixel shader around a body.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="isUnsafe">Whether the entry point needs an unsafe context.</param>
    /// <returns>The source of a pixel shader carrying <paramref name="body"/>.</returns>
    private static string Shader(string body, bool isUnsafe)
    {
        return $$"""
            using System.Linq;
            using ComputeWeave;
            using ComputeWeave.D2D1;

            namespace Shaders;

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct Shader : ID2D1PixelShader
            {
                public {{(isUnsafe ? "unsafe " : "")}}Float4 Execute()
                {
                    int k = 1;

                    {{body}}

                    return k;
                }
            }
            """;
    }
}
