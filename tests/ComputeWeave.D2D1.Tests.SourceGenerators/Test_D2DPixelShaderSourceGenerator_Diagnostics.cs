using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// What the generator for <c>[D2DPixelShaderSource]</c> reports when the shader compiler refuses the text.
/// </summary>
/// <remarks>
/// The text is written by the author and handed to the compiler as it stands, so the failure is theirs to
/// read. What this holds in place is that the failure arrives as a diagnostic on the method rather than as a
/// compile error against the generated source.
/// </remarks>
[TestClass]
public class Test_D2DPixelShaderSourceGenerator_Diagnostics
{
    [TestMethod]
    public void D2DPixelShaderSourceCompilationFailedWithFxcCompilationException()
    {
        const string source = """"
            using System;
            using ComputeWeave.D2D1;

            public partial class MyClass
            {
                [D2DPixelShaderSource("""
                    #define D2D_INPUT_COUNT 0

                    #include "d2d1effecthelpers.hlsli"

                    D2D_PS_ENTRY(Execute)
                    {
                        return notADeclaredName;
                    }
                    """)]
                [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
                [D2DCompileOptions(D2D1CompileOptions.Default)]
                public static partial ReadOnlySpan<byte> InvertEffect();
            }
            """";

        CSharpGeneratorTest<D2DPixelShaderSourceGenerator>.VerifyDiagnosticIsReported(source, "CMPWD2D0054");
    }
}
