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

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0088");
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

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0090");
    }

    /// <summary>
    /// A generic method. The rewriters are shared with the compute generator, so what this pins is that the
    /// pixel shader generator answers with its own identifier.
    /// </summary>
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

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0091");
    }

    /// <summary>
    /// A generic local function that is never called. It is lifted just the same, so the declaration answers
    /// for it. The rewriters are shared with the compute generator, so what this pins is the identifier.
    /// </summary>
    [TestMethod]
    public void DeclaringAGenericLocalFunctionWithoutCallingItIsDiagnosed()
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
                    static float First<T>(T value)
                        where T : unmanaged
                    {
                        return 1.0f;
                    }

                    return new float4(time, 0, 0, 1);
                }
            }
            """;

        // The type parameter list is also recorded as syntax outside the accepted set, so the set is not asserted
        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(source, "CMPWD2D0095");
    }

    /// <summary>
    /// A method declared in a C# extension block. The rewriters are shared with the compute generator, so
    /// what this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
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

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0092");
    }

    /// <summary>
    /// A static field initializer calling a method the generator wrote. FXC accepts it, the forward
    /// declarations being written ahead of the static fields, so the same holds on this path as on the
    /// compute one. This is pinned because what an initializer may call decides how it can be rewritten.
    /// </summary>
    [TestMethod]
    public void AStaticFieldInitializerMayCallAShaderMethod()
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
                private static readonly float Scale = Member(2.0f);

                private readonly float time;

                private static float Member(float value) => value * 2;

                public float4 Execute()
                {
                    return new float4(Scale, this.time, 0, 0);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source);
    }

    /// <summary>
    /// A type declaring a primary constructor. The rewriters are shared with the compute generator, so what
    /// this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// Unlike the other rewriter diagnostics here, no compile error follows it. The construction falls back
    /// to a default value, which is valid HLSL, so the shader still compiles and only this one is reported.
    /// </remarks>
    [TestMethod]
    public void ConstructingATypeWithAPrimaryConstructorIsDiagnosed()
    {
        const string source = """
            using ComputeWeave;
            using ComputeWeave.D2D1;
            using float4 = global::ComputeWeave.Float4;

            namespace MyNamespace;

            internal readonly struct Helper(float value)
            {
                public readonly float Doubled() => value * 2;
            }

            [D2DInputCount(0)]
            [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
            [D2DGeneratedPixelShaderDescriptor]
            internal readonly partial struct MyShader : ID2D1PixelShader
            {
                private readonly float time;

                public float4 Execute()
                {
                    Helper helper = new(this.time);

                    return new float4(helper.Doubled(), 0, 0, 0);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0093");
    }

    /// <summary>
    /// A native integer type has no HLSL counterpart. The rule that refuses it is shared with the compute
    /// path, which covers the pair; what this reads is the diagnostic the Direct2D path maps it to.
    /// </summary>
    [TestMethod]
    public void NativeIntegerType()
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
                public float4 Execute()
                {
                    nint value = 1000;

                    return new float4((float)value, 0, 0, 0);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0041");
    }

    /// <summary>
    /// Syntax outside the set a shader body may use. The rewriter that reports it is shared with the compute
    /// generator, so what this pins is that the pixel shader generator answers with its own identifier.
    /// </summary>
    /// <remarks>
    /// The shader is handed to FXC after the report, the report recording the syntax rather than refusing it,
    /// so the compile error raised on the same construct is named here too.
    /// </remarks>
    [TestMethod]
    public void SyntaxOutsideTheAcceptedSetIsReported()
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
                private readonly int index;

                public float4 Execute()
                {
                    float value = this.index switch { 0 => 1.0f, _ => 2.0f };

                    return new float4(value, 0, 0, 0);
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnostics(source, "CMPWD2D0094", "CMPWD2D0034");
    }

    /// <summary>
    /// A shader that reads the scene position without declaring that it needs it.
    /// </summary>
    /// <remarks>
    /// The attribute changes the signature the effect is registered with, so the shader would run against a
    /// pipeline that never supplies the position.
    /// </remarks>
    [TestMethod]
    public void MissingD2DRequiresScenePositionAttribute()
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
                public float4 Execute()
                {
                    return D2D.GetScenePosition();
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(source, "CMPWD2D0045");
    }

    /// <summary>
    /// A resource texture whose element type is neither a single nor a four component vector.
    /// </summary>
    [TestMethod]
    public void InvalidResourceTextureElementType()
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
                [D2DResourceTextureIndex(0)]
                private readonly D2D1ResourceTexture2D<int> texture;

                public float4 Execute()
                {
                    return 0;
                }
            }
            """;

        CSharpGeneratorTest<D2DPixelShaderDescriptorGenerator>.VerifyDiagnosticIsReported(source, "CMPWD2D0051");
    }
}
