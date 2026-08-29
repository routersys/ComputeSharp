using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests that an integer literal reaches HLSL as its value on the Direct2D path.
/// </summary>
/// <remarks>
/// <para>
/// The Direct2D path compiles with FXC, which accepts fewer spellings than DXC does. A binary literal
/// compiles on the compute path and fails here, so the same shader body used to compile or not
/// depending on which compiler received it. Writing the value removes that difference.
/// </para>
/// <para>
/// The literal is measured twice: once as the generated HLSL text, and once as the color a pixel takes.
/// Text alone does not show that FXC accepts what was written. A value alone would also be produced by
/// a compiler that happened to accept the spelling the author used.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void IntegerLiteral_IsPrintedFromItsValue()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<IntegerLiteralShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                int separated = 1000;
                int binary = 10;
                uint unsignedLarge = 4294967295u;
                bool correct = separated == 1000 && binary == 10 && unsignedLarge == 4294967295u;
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void IntegerLiteral_ComputesTheSameValue()
    {
        D2D1TestRunner.RunAndCompareShader(
            new IntegerLiteralShader(),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Reads back three spellings that used to reach FXC as they were written.
    /// </summary>
    /// <remarks>
    /// The unsigned value is past the signed range, so a suffix dropped on the way out is visible as a
    /// wrong number rather than as a compiler error.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct IntegerLiteralShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            int separated = 1_000;
            int binary = 0b1010;
            uint unsignedLarge = 4_294_967_295u;

            bool correct = separated == 1000 && binary == 10 && unsignedLarge == 4294967295u;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }
}
