using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The conversions C# applies to the arguments of an intrinsic call and to the arms of a conditional,
/// which the shader compiler does not apply for itself. Writing them as they stand lets the compiler
/// resolve the call, or bring the arms together, over the types before the conversion, and an int beside
/// a uint resolves to the unsigned one there while C# chose the floating point one.
/// </summary>
[TestClass]
public class MixedKindArgumentTests
{
    /// <summary>
    /// One shader reaching each shape the rewriting has to answer for: a mixed call through the plain
    /// mapping, a mixed call through a named intrinsic, a call whose arguments already agree, a mixed
    /// call whose two resolutions agree anyway, the same three shapes on a conditional's arms, a
    /// conditional standing inside a larger expression, and one in a static field initializer.
    /// </summary>
    private const string Source = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly int Seeded = -1;

            private static readonly uint Offset = 2;

            private static readonly float Chosen = Seeded < 0 ? Seeded : Offset;

            private readonly ReadWriteBuffer<float> buffer;

            private readonly int negative;

            private readonly uint positive;

            private readonly int count;

            private readonly float scale;

            private readonly Int2 low;

            private readonly UInt2 high;

            private readonly Bool2 mask;

            public void Execute()
            {
                this.buffer[0] = Hlsl.Max(this.negative, this.positive);
                this.buffer[1] = Hlsl.Select(this.mask, this.low, this.high).X;
                this.buffer[2] = Hlsl.Max(this.count, this.count);
                this.buffer[3] = Hlsl.Max(this.count, this.scale);
                this.buffer[4] = this.mask.X ? this.negative : this.positive;
                this.buffer[5] = this.mask.X ? this.count : this.count;
                this.buffer[6] = this.mask.X ? this.count : this.scale;
                this.buffer[7] = this.mask.X ? (this.mask.Y ? this.negative : this.positive) : this.scale;
                this.buffer[8] = Hlsl.Max(this.mask.X ? this.negative : this.positive, this.scale);
                this.buffer[9] = Chosen;
            }
        }
        """;

    /// <summary>
    /// The call C# binds to the floating point overload while HLSL would resolve it to the unsigned one.
    /// </summary>
    [TestMethod]
    public void AMixedCallCarriesTheConversionCSharpApplied()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("max((float)negative, (float)positive)"),
            $"the mixed call does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// The same shape reached through a named intrinsic, which the rewriting answers before it lowers the
    /// call to its own construct. A cast written after that point would never reach this one.
    /// </summary>
    [TestMethod]
    public void AMixedCallToANamedIntrinsicCarriesTheConversion()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("(float2)low") && generated.Contains("(float2)high"),
            $"the mixed call to a named intrinsic does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// A call whose arguments already agree, which has no conversion to carry and must be left as it stands.
    /// </summary>
    [TestMethod]
    public void ACallWhoseArgumentsAgreeIsLeftAlone()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("max(count, count)"),
            $"a call needing no conversion was rewritten:\n{generated}");
    }

    /// <summary>
    /// A mixed call the shader compiler would resolve the same way. The conversion is written out all the
    /// same, the judgment being the type the call binds to rather than the pair of kinds it was given.
    /// </summary>
    [TestMethod]
    public void AMixedCallThatWouldResolveAlikeCarriesTheConversionToo()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("max((float)count, scale)"),
            $"the widening call does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// A conditional with one arm of each integer kind. C# has no natural type for it and brings both arms
    /// to the type it is used as, where the shader compiler brings them to the unsigned kind first.
    /// </summary>
    [TestMethod]
    public void MixedConditionalArmsCarryTheConversionCSharpApplied()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("? (float)negative : (float)positive"),
            $"the mixed conditional does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// A conditional whose arms are already of one kind, which has no conversion to carry.
    /// </summary>
    [TestMethod]
    public void ConditionalArmsOfOneKindAreLeftAlone()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("? count : count"),
            $"a conditional needing no conversion was rewritten:\n{generated}");
    }

    /// <summary>
    /// A conditional the shader compiler would bring together the same way. The conversion is written out
    /// all the same, the judgment being the type of the conditional rather than the pair of kinds in it.
    /// </summary>
    [TestMethod]
    public void ConditionalArmsThatWouldAgreeCarryTheConversionToo()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("? (float)count : scale"),
            $"the widening conditional does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// A conditional standing inside a larger expression, nested in another conditional and given as the
    /// argument of a call. An arm that is itself a conditional of the same type carries no conversion of
    /// its own, and neither does an argument that needed none, so the conversion is written once.
    /// </summary>
    [TestMethod]
    public void AConditionalInsideALargerExpressionCarriesTheConversionOnce()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("mask.x ? (mask.y ? (float)negative : (float)positive) : scale"),
            $"the nested conditional does not carry the conversion once:\n{generated}");

        Assert.IsTrue(
            generated.Contains("max(mask.x ? (float)negative : (float)positive, scale)"),
            $"the conditional given as an argument does not carry the conversion once:\n{generated}");
    }

    /// <summary>
    /// A conditional in a static field initializer, which a rewriter of its own writes out. The rewriting is
    /// declared on the type both rewriters derive from, so what this pins is that the two answer alike.
    /// </summary>
    [TestMethod]
    public void AConditionalInAStaticFieldInitializerCarriesTheConversion()
    {
        string generated = Generate();

        Assert.IsTrue(
            generated.Contains("static const float Chosen = Seeded < 0 ? (float)Seeded : (float)Offset;"),
            $"the conditional in a static field initializer does not carry the conversion:\n{generated}");
    }

    /// <summary>
    /// The aliases a shader compilation carries, which another generator writes there and this one relies on.
    /// A descriptor naming a vector type writes the alias, so a compilation without them does not build.
    /// </summary>
    private const string Aliases = """
        global using bool2 = global::ComputeWeave.Bool2;
        global using float2 = global::ComputeWeave.Float2;
        global using int2 = global::ComputeWeave.Int2;
        global using uint2 = global::ComputeWeave.UInt2;
        """;

    private static string Generate()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [Source, Aliases],
            "ShaderMixedKindArgumentTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Shaders.Shader");
    }
}
