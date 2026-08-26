using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests for the identifiers the Direct2D path has to rename before they reach FXC.
/// </summary>
/// <remarks>
/// The shared list of reserved names is measured against DXC, which the Direct2D path does not use.
/// FXC keeps the effect framework keywords, and matches four of them without regard to case, so a
/// field carrying one of those names reaches FXC unrenamed and the shader fails to compile with an
/// error naming a line of generated code the author never wrote.
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void ReservedFieldNames_AreRenamedForFxc()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<ReservedFieldNameShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            float __reserved__Pass;
            float __reserved__technique;
            float __reserved__ASM;
            float __reserved__SamplerState;
            float __reserved__texture2D;

            D2D_PS_ENTRY(Execute)
            {
                bool correct = __reserved__Pass == 1.0 && __reserved__technique == 2.0 && __reserved__ASM == 3.0 && __reserved__SamplerState == 4.0 && __reserved__texture2D == 5.0;
                return correct ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    [TestMethod]
    public void ReservedFieldNames_CarryTheirValues()
    {
        D2D1TestRunner.RunAndCompareShader(
            new ReservedFieldNameShader(1f, 2f, 3f, 4f, 5f),
            32,
            32,
            "Green32x32.png");
    }

    /// <summary>
    /// Captures five fields whose names FXC rejects. Two of them, <c>Pass</c> and <c>technique</c>,
    /// are keywords FXC matches without regard to case, and <c>ASM</c> is a third one in a casing the
    /// specification never spells. The other two are effect framework names DXC does not reserve.
    /// </summary>
    /// <remarks>
    /// A name that failed to be renamed would not reach this test as a failed assertion. The shader
    /// is compiled while the test project is built, so the failure would be a build error instead.
    /// </remarks>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ReservedFieldNameShader(
        float Pass,
        float technique,
        float ASM,
        float SamplerState,
        float texture2D) : ID2D1PixelShader
    {
        public float4 Execute()
        {
            bool correct =
                Pass == 1f &&
                technique == 2f &&
                ASM == 3f &&
                SamplerState == 4f &&
                texture2D == 5f;

            return correct ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }
    }
}
