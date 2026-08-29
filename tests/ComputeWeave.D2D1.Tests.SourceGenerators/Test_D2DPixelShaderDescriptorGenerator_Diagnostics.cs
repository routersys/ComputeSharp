using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

[TestClass]
public class Test_D2DPixelShaderDescriptorGenerator_Diagnostics
{
    [TestMethod]
    public void MissingD2DRequiresDoublePrecisionSupportAttribute()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    return (float)(time * 2.0);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0080");
    }

    [TestMethod]
    public void UnnecessaryD2DRequiresDoublePrecisionSupportAttribute()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DRequiresDoublePrecisionSupport]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    return (float)(time * 2.0f);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0081");
    }

    /// <summary>
    /// A property read from a custom type. The rewriters are shared with the compute generator, so what
    /// this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// The shader is still handed to FXC after the diagnostic, as it is for every other rewriter
    /// diagnostic, so the compile error it raises on the same read is named here too.
    /// </remarks>
    [TestMethod]
    public void ReadingAPropertyOfACustomTypeIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal struct Helper
            {
                public float Amount;

                public readonly float Doubled => Amount * 2;
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    Helper helper = default;

                    helper.Amount = time;

                    return helper.Doubled;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0088", "CMPWD2D0034");
    }
    /// <summary>
    /// A conversion operator declared on a custom type. The rewriters are shared with the compute generator,
    /// so what this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// No compile error is named alongside it, unlike the other rewriter diagnostics. HLSL converts between
    /// a struct and a scalar on its own, so this shader used to compile and then compute a different value
    /// than the same code in C#. The diagnostic is the only signal there is.
    /// </remarks>
    [TestMethod]
    public void UsingAConversionOperatorOfACustomTypeIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal struct Value
            {
                public float First;

                public float Second;

                public static explicit operator float(Value value) => value.Second;
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    Value value = default;

                    value.First = time;
                    value.Second = time * 2;

                    return (float)value;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0089");
    }
    /// <summary>
    /// An indexer declared on a custom type. The rewriters are shared with the compute generator, so what
    /// this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// The shader is still handed to FXC after the diagnostic, as it is for every other rewriter
    /// diagnostic, so the compile error it raises on the same access is named here too.
    /// </remarks>
    [TestMethod]
    public void UsingAnIndexerOfACustomTypeIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal struct Values
            {
                public float Amount;

                public readonly float this[int index] => Amount;
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    Values values = default;

                    values.Amount = time;

                    return values[0];
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0090", "CMPWD2D0034");
    }

    /// <summary>
    /// A generic method. The rewriters are shared with the compute generator, so what this pins is that the
    /// pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// The shader is still handed to FXC after the diagnostic, as it is for every other rewriter
    /// diagnostic, so the compile error it raises on the same call is named here too.
    /// </remarks>
    [TestMethod]
    public void CallingAGenericMethodIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal static class Helper
            {
                public static float First<T>(T value)
                    where T : unmanaged
                {
                    return 1.0f;
                }
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    return Helper.First(time);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0091", "CMPWD2D0034");
    }

    /// <summary>
    /// A method declared in a C# extension block. The rewriters are shared with the compute generator, so
    /// what this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// The shader is still handed to FXC after the diagnostic, as it is for every other rewriter
    /// diagnostic, so the compile error it raises on the same call is named here too.
    /// </remarks>
    [TestMethod]
    public void CallingAnExtensionMemberIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal static class Helper
            {
                extension(float value)
                {
                    public float Doubled() => value * 2;
                }
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    return this.time.Doubled();
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0092", "CMPWD2D0034");
    }
}
