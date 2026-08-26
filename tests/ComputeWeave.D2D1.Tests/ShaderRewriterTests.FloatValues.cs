using System;
using System.Linq;
using System.Text;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests for how a float value reaches HLSL on the Direct2D path.
/// </summary>
/// <remarks>
/// <para>
/// A float can be written as a literal or held in a constant, and for a while those two took
/// different routes: the literal was written as its bit pattern through <c>asfloat</c>, to work
/// around an FXC defect that can give a decimal literal the wrong value, while the constant was
/// written as decimal text. Both now write decimal text.
/// </para>
/// <para>
/// The reason is the second test here. For the two feature level 9 profiles FXC puts a second
/// translation of the shader in the container, and it is that translation a feature level 9 device
/// runs. In it, <c>asfloat</c> of a literal is folded as a conversion of the number rather than a
/// reinterpretation of its bits, so <c>1.5</c> arrives as <c>1069547520</c>. Decimal text is right in
/// both translations. Since the HLSL a shader carries can be recompiled at run time for any profile,
/// one text has to be right for all of them.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void FloatValue_IsPrintedTheSameWayAsALiteralAndAsAConstant()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<FloatValueShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__Positive 131072.66
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__Negative -131072.66
            #define __ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__NegativeZero -0.0

            D2D_PS_ENTRY(Execute)
            {
                float literal = 131072.66;
                bool correct = literal == __ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__Positive && -literal == __ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__Negative && asint(__ComputeWeave_D2D1_Tests_ShaderRewriterTests_FloatValueShader__NegativeZero) != 0;
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void FloatValue_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new FloatValueShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Compiles the shader for the feature level 9 profiles and reads the value back out of the
    /// second translation FXC puts in the container, the one such a device runs.
    /// </summary>
    /// <remarks>
    /// The expected encodings are computed from IEEE 754, not read back out of the compiler. The
    /// value this guards against, <c>0x4E900000</c>, is what the bit pattern becomes when it is read
    /// as an integer and converted, which is what <c>asfloat</c> was doing here.
    /// </remarks>
    [TestMethod]
    public void FloatValue_SurvivesTheFeatureLevel9Translation()
    {
        const uint expected = 0x4800002A;
        const uint wrong = 0x4E900000;

        Assert.AreEqual(131072.65f, BitConverter.UInt32BitsToSingle(expected), "the expected encoding is not 131072.65");
        Assert.AreEqual(wrong, BitConverter.SingleToUInt32Bits(expected), "the guarded against encoding is not the converted one");

        string hlslSource = D2D1ReflectionServices.GetShaderInfo<FloatValueLevel9Shader>().HlslSource;

        foreach (D2D1ShaderProfile profile in (D2D1ShaderProfile[])[D2D1ShaderProfile.PixelShader40Level91, D2D1ShaderProfile.PixelShader40Level93])
        {
            byte[] bytecode = D2D1ShaderCompiler.Compile(
                hlslSource.AsSpan(),
                "Execute".AsSpan(),
                profile,
                D2D1CompileOptions.Default).ToArray();

            byte[] levelNine = GetChunk(bytecode, "Aon9");

            Assert.IsTrue(levelNine.Length > 0, $"{profile} produced no feature level 9 translation");
            Assert.IsFalse(Holds(levelNine, wrong), $"{profile} holds the converted value instead of the bit pattern");
            Assert.IsTrue(Holds(levelNine, expected), $"{profile} does not hold 131072.65");
        }
    }

    /// <summary>
    /// Reads one chunk out of a DXBC container.
    /// </summary>
    /// <param name="bytecode">The container to read.</param>
    /// <param name="fourCC">The name of the chunk to read.</param>
    /// <returns>The contents of the chunk, or an empty array when the container has no such chunk.</returns>
    private static byte[] GetChunk(byte[] bytecode, string fourCC)
    {
        int chunkCount = BitConverter.ToInt32(bytecode, 28);

        for (int i = 0; i < chunkCount; i++)
        {
            int offset = BitConverter.ToInt32(bytecode, 32 + (i * 4));

            if (Encoding.ASCII.GetString(bytecode, offset, 4) == fourCC)
            {
                return [.. bytecode.Skip(offset + 8).Take(BitConverter.ToInt32(bytecode, offset + 4))];
            }
        }

        return [];
    }

    /// <summary>
    /// Checks whether a chunk holds a given 32 bit value at any offset.
    /// </summary>
    /// <param name="chunk">The chunk to search.</param>
    /// <param name="value">The value to look for.</param>
    /// <returns>Whether <paramref name="value"/> appears in <paramref name="chunk"/>.</returns>
    private static bool Holds(byte[] chunk, uint value)
    {
        for (int i = 0; i + 4 <= chunk.Length; i++)
        {
            if (BitConverter.ToUInt32(chunk, i) == value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The same value as a literal and as a constant, with nothing else in it. The feature level 9
    /// profiles reject <c>asint</c>, so the negative zero of the shader above cannot be asked about
    /// here, and this one carries only what the value test needs.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader40Level91)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct FloatValueLevel9Shader : ID2D1PixelShader
    {
        private const float Constant = 131072.65f;

        public float4 Execute()
        {
            return new float4(131072.65f, Constant, 0, 1);
        }
    }

    /// <summary>
    /// Holds one value three ways: as a literal, as a constant, and as a constant that is negative
    /// zero, which is the case that says where the sign of the value is written.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct FloatValueShader : ID2D1PixelShader
    {
        private const float Positive = 131072.65f;
        private const float Negative = -131072.65f;
        private const float NegativeZero = -0f;

        public float4 Execute()
        {
            float literal = 131072.65f;

            bool correct =
                literal == Positive &&
                -literal == Negative &&
                Hlsl.AsInt(NegativeZero) != 0;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }
}
