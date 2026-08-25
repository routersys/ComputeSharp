using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests that the rewriting fixes recorded against the shared generator reach the Direct2D path.
/// </summary>
/// <remarks>
/// <para>
/// Most of those fixes were made while the Direct2D path compiled no code at all, so whether they
/// reach it is a question about the shared generator rather than something any test had shown. The
/// shared code carries eight conditional regions and none of them holds one of these fixes, so the
/// answer should be that all of them do. This shader is what turns that reading into a measurement.
/// </para>
/// <para>
/// One shader covers ten of them at once: <c>bool</c> constants, discovered types, mapped member
/// replacements, numeric constants of every kind, reserved identifiers, matrix constructor arguments,
/// unsigned right shift operands, hoisted local function names, local functions inside imported
/// methods, and character values. The values are chosen so that the regrouped form of each one gives
/// a different answer, and the shader is green only when every one of them is right.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void LedgerCoverage_IsRewrittenForTheDirect2DPath()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<LedgerCoverageShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Flag true
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Real 1.5L
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Number 1.0
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Letter 65
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__NegativeInfinity asfloat(0xFF800000)
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Whole asfloat(0x3F800000)

            struct ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoveragePoint
            {
                float X;
                float Y;
            };

            static int __Execute__object(int value);

            static int ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled__Twice(int v);

            static int ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled(int value);

            int seed;

            static int __Execute__object(int value)
            {
                return value + 1;
            }

            static int ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled__Twice(int v)
            {
                return v * 2;
            }

            static int ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled(int value)
            {
                return ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled__Twice(value);
            }

            D2D_PS_ENTRY(Execute)
            {
                int shifted = seed;
                shifted = (int)((uint)shifted >> (__ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Flag ? 1 : 2));
                uint value = (uint)seed;
                float2x2 values = float2x2((float)(value / 2), (float)(__ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Real > 1 ? 1 : 0), (float)(float)__ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Number, (float)__ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Letter);
                ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoveragePoint __reserved__point;
                __reserved__point.X = values._m00;
                __reserved__point.Y = values._m11;
                bool correct = shifted == 3 && __reserved__point.X == 3 && __reserved__point.Y == 65 && ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageHelper_Doubled(shifted) == 6 && __Execute__object(1) == 2 && __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Flag && __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Letter == 65 && __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__NegativeInfinity < 0 && __ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Whole / 2 == asfloat(1056964608U) && (float)__ComputeWeave_D2D1_Tests_ShaderRewriterTests_LedgerCoverageShader__Number / 2 == asfloat(1056964608U);
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void LedgerCoverage_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new LedgerCoverageShader(7),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// A helper whose static method carries a static local function of its own.
    /// </summary>
    internal static class LedgerCoverageHelper
    {
        public static int Doubled(int value)
        {
            static int Twice(int v) => v * 2;

            return Twice(value);
        }
    }

    /// <summary>
    /// A custom struct, so that the discovered types collection sees a named type.
    /// </summary>
    internal struct LedgerCoveragePoint
    {
        public float X;
        public float Y;
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct LedgerCoverageShader(int seed) : ID2D1PixelShader
    {
        private const bool Flag = true;
        private const float Whole = 1;
        private const double Real = 1.5;
        private const decimal Number = 1m;
        private const char Letter = 'A';
        private const float NegativeInfinity = float.NegativeInfinity;

        public float4 Execute()
        {
            // A verbatim identifier as the name of a hoisted local function
            static int @object(int value) => value + 1;

            int shifted = seed;

            // The right operand of a compound unsigned right shift. Regrouped it reads as
            // (shifted >> Flag) ? 1 : 2, which is 1 rather than 3.
            shifted >>>= Flag ? 1 : 2;

            // The first argument needs parentheses under the cast the constructor applies. Regrouped
            // it divides in floating point and gives 3.5 rather than 3. The division is unsigned
            // because FXC warns on a signed one, and that warning is an error by default.
            uint value = (uint)seed;
            float2x2 values = new(value / 2, Real > 1 ? 1 : 0, (float)Number, Letter);

            LedgerCoveragePoint point;

            point.X = values.M11;
            point.Y = values.M22;

            bool correct =
                shifted == 3 &&
                point.X == 3 &&
                point.Y == 65 &&
                LedgerCoverageHelper.Doubled(shifted) == 6 &&
                @object(1) == 2 &&
                Flag &&
                Letter == 65 &&
                NegativeInfinity < 0 &&
                Whole / 2 == 0.5f &&
                (float)Number / 2 == 0.5f;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }
}
