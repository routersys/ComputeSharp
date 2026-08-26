using System;
using System.ComponentModel;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests for the nine intrinsics a compute shader may not use.
/// </summary>
/// <remarks>
/// <para>
/// <c>UnsupportedHlslIntrinsicAnalyzer</c> rejects these nine with <c>CMPW0112</c>. It lives in
/// <c>ComputeWeave.SourceGenerators</c>, which no D2D1 consumer loads, so the rejection does not
/// reach the authoring path. A pixel shader has pixels to discard and a stage that computes
/// derivatives, so eight of the nine are ordinary here.
/// </para>
/// <para>
/// <c>Abort</c> is the exception, and not for the compute-side reason. D2D1 compiles a shader twice
/// when linking is enabled, which the default options do: once as a full pixel shader (<c>ps_*</c>)
/// and once as an export function (<c>lib_*</c>). FXC classes <c>abort</c> as a debug instruction
/// and refuses it in a library, so the second compilation fails with <c>X4602</c>. Turning linking
/// off leaves only the pixel shader compilation, which succeeds, but D2D1 then refuses to create an
/// effect from the result. <c>Abort</c> is therefore unusable in a D2D1 effect by either route, and
/// the tests below pin both halves against controls that isolate the instruction from the options.
/// </para>
/// <para>
/// That this file compiles at all is half the measurement: this project loads
/// <c>ComputeWeave.D2D1.SourceGenerators</c> and <c>ComputeWeave.Core.SourceGenerators</c> as
/// analyzers, and a <c>CMPW0112</c> reaching here would be an error. The other half is the
/// compute-side counterpart, <c>UnsupportedHlslIntrinsicAnalyzerTests</c>, which asserts that the
/// same nine names do produce <c>CMPW0112</c>. Without it, this test would also pass if the
/// analyzer had simply stopped working.
/// </para>
/// </remarks>
[TestClass]
public partial class ComputeOnlyIntrinsicsTests
{
    /// <summary>
    /// The body of a D2D1 effect, with a placeholder for a statement to test.
    /// </summary>
    private const string EffectSource = """
        #define D2D_INPUT_COUNT 0
        #define D2D_REQUIRES_SCENE_POSITION

        #include "d2d1effecthelpers.hlsli"

        D2D_PS_ENTRY(Execute)
        {
            float x = D2DGetScenePosition().x;
            BODY
            return float4(0, 1, 0, 1);
        }
        """;

    [TestMethod]
    public void SevenDerivativesAndClip_CompileUnderTheDefaultOptions()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<DerivativesAndClipShader>();

        Assert.IsTrue(shaderInfo.HlslBytecode.Length > 0, "the shader produced no bytecode");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddx("), "ddx did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddy("), "ddy did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddx_coarse("), "ddx_coarse did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddy_coarse("), "ddy_coarse did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddx_fine("), "ddx_fine did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("ddy_fine("), "ddy_fine did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("fwidth("), "fwidth did not reach the HLSL");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("clip("), "clip did not reach the HLSL");
    }

    [TestMethod]
    public void SevenDerivativesAndClip_ComputeTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new DerivativesAndClipShader(),
            32,
            32,
            "Green32x32.png");
    }

    [TestMethod]
    public void Abort_CompilesWhenLinkingIsDisabled()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<AbortWithoutLinkingShader>();

        Assert.IsTrue(shaderInfo.HlslBytecode.Length > 0, "the shader produced no bytecode");
        Assert.IsTrue(shaderInfo.HlslSource.Contains("abort()"), "abort did not reach the HLSL");
    }

    /// <summary>
    /// The bytecode compiles, but D2D1 will not build an effect out of it. Abort is therefore not
    /// usable in a D2D1 effect by any route: with linking it fails to compile, without linking it
    /// compiles and then fails to load.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(Win32Exception))]
    public void Abort_EffectCannotBeCreated()
    {
        D2D1TestRunner.RunAndCompareShader(
            new AbortWithoutLinkingShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// The control for <see cref="Abort_EffectCannotBeCreated"/>: the same compile options without
    /// the abort produce an effect that loads and draws, so it is the instruction that D2D1 refuses
    /// and not the absence of the linked blob.
    /// </summary>
    [TestMethod]
    public void EffectWithoutAbort_LoadsWhenLinkingIsDisabled()
    {
        D2D1TestRunner.RunAndCompareShader(
            new NoLinkingShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// The control for <see cref="Abort_IsRejectedByTheLibraryCompilation"/>: the same effect without
    /// the <c>abort</c> compiles under the same options, so the rejection is about the instruction.
    /// </summary>
    [TestMethod]
    public void EffectWithoutAbort_CompilesWithLinking()
    {
        ReadOnlyMemory<byte> bytecode = D2D1ShaderCompiler.Compile(
            EffectSource.Replace("BODY", "clip(x + 1.0);").AsSpan(),
            "Execute".AsSpan(),
            D2D1ShaderProfile.PixelShader50,
            D2D1CompileOptions.Default);

        Assert.IsTrue(bytecode.Length > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(FxcCompilationException))]
    public void Abort_IsRejectedByTheLibraryCompilation()
    {
        _ = D2D1ShaderCompiler.Compile(
            EffectSource.Replace("BODY", "if (x < -1.0) { abort(); }").AsSpan(),
            "Execute".AsSpan(),
            D2D1ShaderProfile.PixelShader50,
            D2D1CompileOptions.Default);
    }

    /// <summary>
    /// The same source as <see cref="Abort_IsRejectedByTheLibraryCompilation"/>, compiled without
    /// linking so that only the pixel shader target is built. FXC accepts <c>abort</c> there.
    /// </summary>
    [TestMethod]
    public void Abort_IsAcceptedByThePixelShaderCompilation()
    {
        ReadOnlyMemory<byte> bytecode = D2D1ShaderCompiler.Compile(
            EffectSource.Replace("BODY", "if (x < -1.0) { abort(); }").AsSpan(),
            "Execute".AsSpan(),
            D2D1ShaderProfile.PixelShader50,
            D2D1CompileOptions.Default & ~D2D1CompileOptions.EnableLinking);

        Assert.IsTrue(bytecode.Length > 0);
    }

    /// <summary>
    /// Uses the eight intrinsics that <c>CMPW0112</c> rejects and D2D1 accepts by default.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct DerivativesAndClipShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            float x = D2D.GetScenePosition().X;

            float sum =
                Hlsl.DerivativeOfDx(x) +
                Hlsl.DerivativeOfDxHighPrecision(x) +
                Hlsl.DerivativeOfDxLowPrecision(x) +
                Hlsl.DerivativeOfDy(x) +
                Hlsl.DerivativeOfDyHighPrecision(x) +
                Hlsl.DerivativeOfDyLowPrecision(x) +
                Hlsl.Fwidth(x);

            // The scene position is never negative, so this never discards
            Hlsl.Clip(x + 1.0f);

            return sum > -1000000.0f ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// The control shader: same compile options as <see cref="AbortWithoutLinkingShader"/>, no abort.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DCompileOptions(D2D1CompileOptions.OptimizationLevel3 | D2D1CompileOptions.WarningsAreErrors | D2D1CompileOptions.PackMatrixRowMajor)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct NoLinkingShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            float x = D2D.GetScenePosition().X;

            return x >= 0 ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// Uses the ninth intrinsic, with linking turned off so that no library is built.
    /// </summary>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DRequiresScenePosition]
    [D2DCompileOptions(D2D1CompileOptions.OptimizationLevel3 | D2D1CompileOptions.WarningsAreErrors | D2D1CompileOptions.PackMatrixRowMajor)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct AbortWithoutLinkingShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            float x = D2D.GetScenePosition().X;

            // The scene position is never negative, so this never fires
            if (x < -1.0f)
            {
                Hlsl.Abort();
            }

            return new float4(0, 1, 0, 1);
        }
    }
}
