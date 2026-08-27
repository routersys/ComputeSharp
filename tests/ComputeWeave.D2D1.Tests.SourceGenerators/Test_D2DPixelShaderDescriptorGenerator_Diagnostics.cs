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
}