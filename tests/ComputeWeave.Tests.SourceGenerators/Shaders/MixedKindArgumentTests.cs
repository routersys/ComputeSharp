using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The conversions C# applies to the arguments of an intrinsic call, which the shader compiler does not
/// apply for itself. Writing the argument as it stands lets the compiler resolve the call again over the
/// types before the conversion, and an int beside a uint resolves to the unsigned overload there while C#
/// chose the floating point one.
/// </summary>
[TestClass]
public class MixedKindArgumentTests
{
    /// <summary>
    /// One shader reaching each shape the rewriting has to answer for: a mixed call through the plain
    /// mapping, a mixed call through a named intrinsic, a call whose arguments already agree, and a mixed
    /// call whose two resolutions agree anyway.
    /// </summary>
    private const string Source = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
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
