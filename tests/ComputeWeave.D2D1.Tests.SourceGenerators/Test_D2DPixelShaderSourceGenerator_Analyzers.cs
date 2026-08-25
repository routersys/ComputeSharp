using System.Threading.Tasks;
using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

[TestClass]
public class Test_D2DPixelShaderSourceGenerator_Analyzers
{
    [TestMethod]
    public async Task InvalidD2DPixelShaderSourceMethodReturnType_ReadOnlySpanOfByte_DoesNotWarn()
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
                        return 0;
                    }
                    """)]
                public static partial ReadOnlySpan<byte> InvertEffect();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<InvalidD2DPixelShaderSourceMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    [DataRow("TimeSpan")]
    [DataRow("ReadOnlyMemory<byte>")]
    [DataRow("ReadOnlySpan<int>")]
    [DataRow("string")]
    [DataRow("object")]
    [DataRow("byte")]
    [DataRow("dynamic")]
    [DataRow("byte*")]
    public async Task InvalidD2DPixelShaderSourceMethodReturnType_InvalidType_Warns(string returnType)
    {
        string source = $$""""
            using System;
            using ComputeWeave.D2D1;

            public unsafe partial class MyClass
            {
                [D2DPixelShaderSource("""
                    #define D2D_INPUT_COUNT 0

                    #include "d2d1effecthelpers.hlsli"

                    D2D_PS_ENTRY(Execute)
                    {
                        return 0;
                    }
                    """)]
                public static partial {{returnType}} {|CMPWD2D0057:InvertEffect|}();

                public static partial {{returnType}} InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<InvalidD2DPixelShaderSourceMethodReturnTypeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingShaderProfileForD2DPixelShaderSource_ShaderProfileOnMethod_DoesNotWarn()
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
                        return 0;
                    }
                    """)]
                [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
                public static partial ReadOnlySpan<byte> InvertEffect();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DShaderProfileOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingShaderProfileForD2DPixelShaderSource_ShaderProfileOnAssembly_DoesNotWarn()
    {
        const string source = """"
            using System;
            using ComputeWeave.D2D1;

            [assembly: D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]

            public partial class MyClass
            {
                [D2DPixelShaderSource("""
                    #define D2D_INPUT_COUNT 0

                    #include "d2d1effecthelpers.hlsli"

                    D2D_PS_ENTRY(Execute)
                    {
                        return 0;
                    }
                    """)]
                public static partial ReadOnlySpan<byte> InvertEffect();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DShaderProfileOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingShaderProfileForD2DPixelShaderSource_MissingShaderProfile_Warns()
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
                        return 0;
                    }
                    """)]
                public static partial ReadOnlySpan<byte> {|CMPWD2D0055:InvertEffect|}();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DShaderProfileOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingCompileOptionsForD2DPixelShaderSource_CompileOptionsOnMethod_DoesNotWarn()
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
                        return 0;
                    }
                    """)]
                [D2DCompileOptions(D2D1CompileOptions.OptimizationLevel0)]
                public static partial ReadOnlySpan<byte> InvertEffect();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DCompileOptionsOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingCompileOptionsForD2DPixelShaderSource_CompileOptionsOnAssembly_DoesNotWarn()
    {
        const string source = """"
            using System;
            using ComputeWeave.D2D1;

            [assembly: D2DCompileOptions(D2D1CompileOptions.OptimizationLevel0)]

            public partial class MyClass
            {
                [D2DPixelShaderSource("""
                    #define D2D_INPUT_COUNT 0

                    #include "d2d1effecthelpers.hlsli"

                    D2D_PS_ENTRY(Execute)
                    {
                        return 0;
                    }
                    """)]
                public static partial ReadOnlySpan<byte> InvertEffect();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DCompileOptionsOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MissingCompileOptionsForD2DPixelShaderSource_MissingCompileOptions_Warns()
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
                        return 0;
                    }
                    """)]
                public static partial ReadOnlySpan<byte> {|CMPWD2D0056:InvertEffect|}();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """";

        await CSharpAnalyzerTest<MissingD2DCompileOptionsOnD2DPixelShaderSourceMethodAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task InvalidD2DPixelShaderSource_NullArgument_Warns()
    {
        const string source = """
            using System;
            using ComputeWeave.D2D1;

            public partial class MyClass
            {
                [D2DPixelShaderSource(null)]
                public static partial ReadOnlySpan<byte> {|CMPWD2D0052:InvertEffect|}();

                public static partial ReadOnlySpan<byte> InvertEffect() => default;
            }
            """;

        await CSharpAnalyzerTest<InvalidD2DPixelShaderSourceAttributeUseAnalyzer>.VerifyAnalyzerAsync(source);
    }
}