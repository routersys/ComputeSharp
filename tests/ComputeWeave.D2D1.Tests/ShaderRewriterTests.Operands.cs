using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests for the operands the D2D1 path embeds into HLSL operators.
/// </summary>
/// <remarks>
/// <para>
/// The shaders here pass a conditional expression where the existing tests pass an identifier. An
/// identifier binds more tightly than any operator, so it reads back the same whether or not it was
/// parenthesized, and a test built on one cannot see whether the operand was parenthesized at all.
/// </para>
/// <para>
/// Each intrinsic is measured twice: once as the generated HLSL text, and once as the value a pixel
/// actually takes. Text alone assumes how the compiler groups the printed tokens. Values alone can be
/// satisfied by an input for which both groupings agree.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void KnownNamedIntrinsic_And_ConditionalOperandIsParenthesized()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<AndOperandRegroupingShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0
            #define D2D_REQUIRES_SCENE_POSITION

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                bool flag = D2DGetScenePosition().x >= 0;
                bool4 t = bool4(true, true, true, true);
                bool4 f = bool4(false, false, false, false);
                bool4 result = ((flag ? t : f) && f);
                return result.x ? float4(1, 0, 0, 1) : float4(0, 1, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void KnownNamedIntrinsic_And_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new AndOperandRegroupingShader(),
            32,
            32,
            "Green32x32.png");
    }

    [TestMethod]
    public void KnownNamedIntrinsic_Or_ConditionalOperandIsParenthesized()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<OrOperandRegroupingShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0
            #define D2D_REQUIRES_SCENE_POSITION

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                bool flag = D2DGetScenePosition().x >= 0;
                bool4 t = bool4(true, true, true, true);
                bool4 f = bool4(false, false, false, false);
                bool4 result = ((flag ? f : t) || t);
                return result.x ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void KnownNamedIntrinsic_Or_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new OrOperandRegroupingShader(),
            32,
            32,
            "Green32x32.png");
    }

    [TestMethod]
    public void KnownNamedIntrinsic_Select_ConditionalConditionIsParenthesized()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<SelectConditionRegroupingShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0
            #define D2D_REQUIRES_SCENE_POSITION

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                bool flag = D2DGetScenePosition().x >= 0;
                bool4 mask = bool4(true, false, true, false);
                bool4 other = bool4(false, false, false, false);
                float4 a = float4(asfloat(1048576000U), asfloat(1048576000U), asfloat(1048576000U), asfloat(1048576000U));
                float4 b = float4(asfloat(1061158912U), asfloat(1061158912U), asfloat(1061158912U), asfloat(1061158912U));
                float4 result = ((flag ? mask : other) ? a : b);
                bool correct = abs(result.x - asfloat(1048576000U)) < asfloat(1008981770U) && abs(result.y - asfloat(1061158912U)) < asfloat(1008981770U);
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void KnownNamedIntrinsic_Select_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new SelectConditionRegroupingShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Fixes the float literal path, which prints a literal that has no exact decimal form as the
    /// bit pattern it actually has, reinterpreted through <c>asfloat</c>.
    /// </summary>
    /// <remarks>
    /// The four constants are the IEEE 754 single precision encodings of 0.25, 0.75, 0.01 and
    /// 131072.65. They are computed from the encoding, not read back out of the generator.
    /// </remarks>
    [TestMethod]
    public void FloatLiteral_IsPrintedThroughAsFloat()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<AsFloatLiteralShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                float value = asfloat(1207959594U);
                return value == asfloat(1207959594U) ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void FloatLiteral_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new AsFloatLiteralShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Fixes the float constant path, which reached HLSL as decimal text while the literal path with
    /// the same value reached it as a bit pattern.
    /// </summary>
    /// <remarks>
    /// The three constants are the IEEE 754 single precision encodings of 131072.65, its negation and
    /// negative zero. They are computed from the encoding, not read back out of the generator. The
    /// sign is printed outside the call, which is where a negated literal already leaves it.
    /// </remarks>
    [TestMethod]
    public void FloatConstant_IsPrintedThroughAsFloat()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<AsFloatConstantShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__Positive asfloat(0x4800002A)
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__Negative -asfloat(0x4800002A)
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__NegativeZero -asfloat(0x00000000)

            D2D_PS_ENTRY(Execute)
            {
                bool correct = __ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__Positive == asfloat(1207959594U) && __ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__Negative == -asfloat(1207959594U) && asint(__ComputeWeave_D2D1_Tests_ShaderRewriterTests_AsFloatConstantShader__NegativeZero) != 0;
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void FloatConstant_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new AsFloatConstantShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Embeds a conditional expression as the first operand of <c>Hlsl.And</c>.
    /// </summary>
    /// <remarks>
    /// Correct: <c>And(t, f)</c> is false, so the shader is green. Printed without parentheses the
    /// operand becomes <c>flag ? t : (f &amp;&amp; f)</c>, which is <c>t</c>, and the shader is red.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct AndOperandRegroupingShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            // Not foldable at compile time, and always true at run time
            bool flag = D2D.GetScenePosition().X >= 0;

            bool4 t = new(true, true, true, true);
            bool4 f = new(false, false, false, false);

            bool4 result = Hlsl.And(flag ? t : f, f);

            return result.X ? new float4(1, 0, 0, 1) : new float4(0, 1, 0, 1);
        }
    }

    /// <summary>
    /// Embeds a conditional expression as the first operand of <c>Hlsl.Or</c>.
    /// </summary>
    /// <remarks>
    /// Correct: <c>Or(f, t)</c> is true, so the shader is green. Printed without parentheses the
    /// operand becomes <c>flag ? f : (t || t)</c>, which is <c>f</c>, and the shader is red.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct OrOperandRegroupingShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            bool flag = D2D.GetScenePosition().X >= 0;

            bool4 t = new(true, true, true, true);
            bool4 f = new(false, false, false, false);

            bool4 result = Hlsl.Or(flag ? f : t, t);

            return result.X ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// Embeds a conditional expression as the condition of <c>Hlsl.Select</c>.
    /// </summary>
    /// <remarks>
    /// Correct: <c>Select(mask, a, b)</c> takes 0.25 from a where the mask is set and 0.75 from b
    /// where it is not. Printed without parentheses the condition becomes
    /// <c>flag ? mask : (other ? a : b)</c>, which is the mask itself widened to a float4.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct SelectConditionRegroupingShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            bool flag = D2D.GetScenePosition().X >= 0;

            bool4 mask = new(true, false, true, false);
            bool4 other = new(false, false, false, false);

            float4 a = new(0.25f, 0.25f, 0.25f, 0.25f);
            float4 b = new(0.75f, 0.75f, 0.75f, 0.75f);

            float4 result = Hlsl.Select(flag ? mask : other, a, b);

            bool correct =
                Hlsl.Abs(result.X - 0.25f) < 0.01f &&
                Hlsl.Abs(result.Y - 0.75f) < 0.01f;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// Exercises the float literal path, which rewrites a literal that cannot be printed exactly
    /// into a bit pattern reinterpreted through <c>asfloat</c>.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DOutputBuffer(D2D1BufferPrecision.Float32)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct AsFloatLiteralShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            float value = 131072.65f;

            return value == 131072.65f ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// Exercises the float constant path, which rewrites a constant into the bit pattern it actually
    /// has, so that a value cannot depend on how the compiler reads decimal text.
    /// </summary>
    /// <remarks>
    /// The negative zero is what fixes where the sign is printed. FXC flushes to zero every value it
    /// cannot represent, and it drops the sign while doing so when the sign is inside the bit pattern.
    /// Printed as <c>asfloat(0x80000000)</c> the constant reaches the shader as positive zero and the
    /// shader is red; printed as <c>-asfloat(0x00000000)</c> it keeps its sign and the shader is green.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DOutputBuffer(D2D1BufferPrecision.Float32)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct AsFloatConstantShader : ID2D1PixelShader
    {
        private const float Positive = 131072.65f;
        private const float Negative = -131072.65f;
        private const float NegativeZero = -0f;

        public float4 Execute()
        {
            bool correct =
                Positive == 131072.65f &&
                Negative == -131072.65f &&
                Hlsl.AsInt(NegativeZero) != 0;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }
}
