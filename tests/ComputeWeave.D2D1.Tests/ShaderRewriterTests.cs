using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA1822

namespace ComputeWeave.D2D1.Tests;

[TestClass]
public partial class ShaderRewriterTests
{
    // See https://github.com/routersys/ComputeWeave/issues/725
    [TestMethod]
    public void ShaderWithStructAndInstanceMethodUsingIt_IsRewrittenCorrectly()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<ClassWithShader.ShaderWithStructAndInstanceMethodUsingIt>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            struct ComputeWeave_D2D1_Tests_ShaderRewriterTests_ClassWithShader_ShaderWithStructAndInstanceMethodUsingIt_Data
            {
                int value;
            };

            void UseData(inout ComputeWeave_D2D1_Tests_ShaderRewriterTests_ClassWithShader_ShaderWithStructAndInstanceMethodUsingIt_Data data);

            void UseData(inout ComputeWeave_D2D1_Tests_ShaderRewriterTests_ClassWithShader_ShaderWithStructAndInstanceMethodUsingIt_Data data)
            {
                ++data.value;
            }

            D2D_PS_ENTRY(Execute)
            {
                ComputeWeave_D2D1_Tests_ShaderRewriterTests_ClassWithShader_ShaderWithStructAndInstanceMethodUsingIt_Data data = (ComputeWeave_D2D1_Tests_ShaderRewriterTests_ClassWithShader_ShaderWithStructAndInstanceMethodUsingIt_Data)0;
                UseData(data);
                return float4(data.value, data.value, data.value, data.value);
            }
            """, shaderInfo.HlslSource);
    }

    internal sealed partial class ClassWithShader
    {
        [D2DInputCount(0)]
        [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
        [D2DGeneratedPixelShaderDescriptor]
        internal readonly partial struct ShaderWithStructAndInstanceMethodUsingIt : ID2D1PixelShader
        {
            private struct Data
            {
                public int value;
            }

            public float4 Execute()
            {
                Data data = default;

                UseData(ref data);

                return new float4(data.value, data.value, data.value, data.value);
            }

            private void UseData(ref Data data)
            {
                ++data.value;
            }
        }
    }

    // See https://github.com/routersys/ComputeWeave/issues/735
    [TestMethod]
    public void KnownNamedIntrinsic_ConditionalSelect()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<KnownNamedIntrinsic_ConditionalSelectShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                bool4 mask4 = bool4(true, false, true, true);
                float4 float4_1 = float4(1, 2, 3.14, 4);
                float4 float4_2 = float4(5, 6, 7, 8);
                float4 float4_r = (mask4 ? float4_1 : float4_2);
                bool1x3 mask1x3 = bool1x3((bool)true, (bool)true, (bool)false);
                int1x3 int1x3_1 = int1x3((int)1, (int)2, (int)3);
                int1x3 int1x3_2 = int1x3((int)4, (int)5, (int)6);
                int1x3 int1x3_r = (mask1x3 ? int1x3_1 : int1x3_2);
                bool2x4 mask2x4 = bool2x4((bool)true, (bool)true, (bool)false, (bool)true, (bool)false, (bool)false, (bool)false, (bool)true);
                uint2x4 uint2x4_1 = uint2x4((uint)1, (uint)2, (uint)3, (uint)4, (uint)5, (uint)6, (uint)7, (uint)8);
                uint2x4 uint2x4_2 = uint2x4((uint)111, (uint)222, (uint)333, (uint)444, (uint)555, (uint)666, (uint)777, (uint)888);
                uint2x4 uint2x4_r = (mask2x4 ? uint2x4_1 : uint2x4_2);
                float2x2 f2x2_r = mul((float2x4)uint2x4_r, float4x2((float2)float4_r.xy, (float2)float4_r.zw, (float2)float2(int1x3_r._m00, int1x3_r._m01), (float2)float2(int1x3_r._m02, 1.0)));
                return float4(f2x2_r._m00, f2x2_r._m01, f2x2_r._m10, f2x2_r._m11);
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct KnownNamedIntrinsic_ConditionalSelectShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            bool4 mask4 = new(true, false, true, true);
            float4 float4_1 = new(1, 2, 3.14f, 4);
            float4 float4_2 = new(5, 6, 7, 8);

            float4 float4_r = Hlsl.Select(mask4, float4_1, float4_2);

            bool1x3 mask1x3 = new(true, true, false);
            int1x3 int1x3_1 = new(1, 2, 3);
            int1x3 int1x3_2 = new(4, 5, 6);

            int1x3 int1x3_r = Hlsl.Select(mask1x3, int1x3_1, int1x3_2);

            bool2x4 mask2x4 = new(true, true, false, true, false, false, false, true);
            uint2x4 uint2x4_1 = new(1, 2, 3, 4, 5, 6, 7, 8);
            uint2x4 uint2x4_2 = new(111, 222, 333, 444, 555, 666, 777, 888);

            uint2x4 uint2x4_r = Hlsl.Select(mask2x4, uint2x4_1, uint2x4_2);

            float2x2 f2x2_r = (float2x4)uint2x4_r * new float4x2(float4_r.XY, float4_r.ZW, new float2(int1x3_r.M11, int1x3_r.M12), new float2(int1x3_r.M13, 1.0f));

            return new(f2x2_r.M11, f2x2_r.M12, f2x2_r.M21, f2x2_r.M22);
        }
    }

    /// <summary>
    /// The conversions C# applies to the arguments of an intrinsic call, which the shader compiler does not
    /// apply for itself. The rewriting is shared with the compute generator, so what this pins is that the
    /// pixel shader generator writes the same conversions out, the named intrinsics it lowers included, and
    /// that a static field initializer answers the same way as the body.
    /// </summary>
    [TestMethod]
    public void MixedKindArguments_AreRewrittenCorrectly()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<MixedKindArgumentsShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            static const int Seeded = -1;
            static const uint Offset = 2;
            static const float Widest = max((float)Seeded, (float)Offset);

            int negative;
            uint positive;
            int count;
            float scale;
            int2 low;
            uint2 high;
            bool2 mask;

            D2D_PS_ENTRY(Execute)
            {
                float mixed = max((float)negative, (float)positive);
                float named = (mask ? (float2)low : (float2)high).x;
                float alike = max(count, count);
                float widened = max((float)count, scale);
                return float4(mixed, named, alike, widened + Widest);
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct MixedKindArgumentsShader(int negative, uint positive, int count, float scale, int2 low, uint2 high, bool2 mask) : ID2D1PixelShader
    {
        private static readonly int Seeded = -1;

        private static readonly uint Offset = 2;

        private static readonly float Widest = Hlsl.Max(Seeded, Offset);

        public float4 Execute()
        {
            float mixed = Hlsl.Max(negative, positive);
            float named = Hlsl.Select(mask, low, high).X;
            float alike = Hlsl.Max(count, count);
            float widened = Hlsl.Max(count, scale);

            return new(mixed, named, alike, widened + Widest);
        }
    }

    [TestMethod]
    public void KnownNamedIntrinsic_AndOr()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<KnownNamedIntrinsic_AndOrShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            D2D_PS_ENTRY(Execute)
            {
                bool4 mask4_1 = bool4(true, false, true, true);
                bool4 mask4_2 = bool4(true, false, true, true);
                bool4 mask4_r_and = (mask4_1 && mask4_2);
                bool4 mask4_r_or = (mask4_1 || mask4_2);
                bool2x3 mask2x3_1 = bool2x3((bool)true, (bool)false, (bool)true, (bool)true, (bool)false, (bool)false);
                bool2x3 mask2x3_2 = bool2x3((bool)true, (bool)false, (bool)true, (bool)true, (bool)true, (bool)true);
                bool2x3 mask2x3_r_and = (mask2x3_1 && mask2x3_2);
                bool2x3 mask2x3_r_or = (mask2x3_1 || mask2x3_2);
                return float4(mask4_r_and.x ? 1 : 0, mask4_r_or.y ? 1 : 0, mask2x3_r_and._m00 ? 1 : 0, mask2x3_r_or._m00 ? 1 : 0);
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct KnownNamedIntrinsic_AndOrShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            bool4 mask4_1 = new(true, false, true, true);
            bool4 mask4_2 = new(true, false, true, true);
            bool4 mask4_r_and = Hlsl.And(mask4_1, mask4_2);
            bool4 mask4_r_or = Hlsl.Or(mask4_1, mask4_2);

            bool2x3 mask2x3_1 = new(true, false, true, true, false, false);
            bool2x3 mask2x3_2 = new(true, false, true, true, true, true);
            bool2x3 mask2x3_r_and = Hlsl.And(mask2x3_1, mask2x3_2);
            bool2x3 mask2x3_r_or = Hlsl.Or(mask2x3_1, mask2x3_2);

            return new(
                mask4_r_and.X ? 1 : 0,
                mask4_r_or.Y ? 1 : 0,
                mask2x3_r_and.M11 ? 1 : 0,
                mask2x3_r_or.M11 ? 1 : 0);
        }
    }

    /// <summary>
    /// A conditional with one arm of each integer kind, which C# brings to the type the conditional is used
    /// as. The rewriting is shared with the compute generator, so what this pins is that the pixel shader
    /// generator writes the same conversion out, in a static field initializer as well as in the body.
    /// </summary>
    [TestMethod]
    public void MixedKindConditionalArms_AreRewrittenCorrectly()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<MixedKindConditionalArmsShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            static const int Seeded = -1;
            static const uint Offset = 2;
            static const float Chosen = Seeded < 0 ? (float)Seeded : (float)Offset;

            int negative;
            uint positive;
            float scale;
            bool flag;

            D2D_PS_ENTRY(Execute)
            {
                float mixed = flag ? (float)negative : (float)positive;
                float widened = flag ? (float)negative : scale;
                int alike = flag ? negative : negative;
                return float4(mixed, widened, alike, Chosen);
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct MixedKindConditionalArmsShader(int negative, uint positive, float scale, bool flag) : ID2D1PixelShader
    {
        private static readonly int Seeded = -1;

        private static readonly uint Offset = 2;

        private static readonly float Chosen = Seeded < 0 ? Seeded : Offset;

        public float4 Execute()
        {
            float mixed = flag ? negative : positive;
            float widened = flag ? negative : scale;
            int alike = flag ? negative : negative;

            return new(mixed, widened, alike, Chosen);
        }
    }

    // See https://github.com/routersys/ComputeWeave/issues/780
    [TestMethod]
    public void ProblematicFloatLiteralValue_IsRewrittenCorrectly()
    {
        D2D1TestRunner.RunAndCompareShader(
            new ProblematicFloatLiteralValueShader(131072.65f),
            32,
            32,
            "Green32x32.png");
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DOutputBuffer(D2D1BufferPrecision.Float32)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ProblematicFloatLiteralValueShader(float x) : ID2D1PixelShader
    {
        public float4 Execute()
        {
            if (x != 131072.65f)
            {
                return 0;
            }

            return new float4(0.0f, 1.0f, 0.0f, 1.0f);
        }
    }

    // See https://github.com/routersys/ComputeWeave/issues/855
    [TestMethod]
    public void ExplicitEntryPointMethodPixelShader_IsRewrittenCorrectly()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<ExplicitEntryPointMethodPixelShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0
            
            #include "d2d1effecthelpers.hlsli"
            
            D2D_PS_ENTRY(Execute)
            {
                return (float4)0;
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ExplicitEntryPointMethodPixelShader : ID2D1PixelShader
    {
        float4 ID2D1PixelShader.Execute()
        {
            return default;
        }
    }

    // See https://github.com/Sergio0694/ComputeSharp/issues/929
    [TestMethod]
    public void D2DSampleInputIntrinsics_WithCompoundExpressions_AreRewrittenCorrectly()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<D2DSampleInputIntrinsicsWithCompoundExpressionsShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 1
            #define D2D_INPUT0_COMPLEX
            #define D2D_REQUIRES_SCENE_POSITION

            #include "d2d1effecthelpers.hlsli"

            float2 samplePosition;

            D2D_PS_ENTRY(Execute)
            {
                float2 scenePosition = D2DGetScenePosition().xy;
                float4 uv = D2DGetInputCoordinate(0);
                float4 a = D2DSampleInput(0, (uv.xy + samplePosition));
                float4 b = D2DSampleInputAtOffset(0, (samplePosition - scenePosition));
                float4 c = D2DSampleInputAtPosition(0, (samplePosition - scenePosition));
                return a + b + c;
            }
            """, shaderInfo.HlslSource);
    }

    [D2DInputCount(1)]
    [D2DInputComplex(0)]
    [D2DRequiresScenePosition]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct D2DSampleInputIntrinsicsWithCompoundExpressionsShader(float2 samplePosition) : ID2D1PixelShader
    {
        public float4 Execute()
        {
            float2 scenePosition = D2D.GetScenePosition().XY;
            float4 uv = D2D.GetInputCoordinate(0);

            float4 a = D2D.SampleInput(0, uv.XY + samplePosition);
            float4 b = D2D.SampleInputAtOffset(0, samplePosition - scenePosition);
            float4 c = D2D.SampleInputAtPosition(0, samplePosition - scenePosition);

            return a + b + c;
        }
    }
}