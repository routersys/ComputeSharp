using ComputeWeave.D2D1.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The discovered type needs an explicit constructor, as the generator rejects primary constructors on one
#pragma warning disable IDE0290

// One of the types below needs the expression bodied constructor this rule forbids, which is the shape under test
#pragma warning disable IDE0021

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// What a static field initializer writes on the Direct2D path.
/// </summary>
/// <remarks>
/// A different rewriter handles those initializers from the one that handles the shader body, and both
/// generators share it, so what it decides has to reach this path too. Where the declarations it imports
/// land among the ones already written is decided by this generator rather than by the rewriter, and this
/// shader is compiled through FXC, so that ordering is measured here rather than read.
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void StaticFieldInitializerConstructor_IsRewrittenForTheDirect2DPath()
    {
        D2D1ShaderInfo shaderInfo = D2D1ReflectionServices.GetShaderInfo<StaticFieldInitializerShader>();

        Assert.AreEqual("""
            #define D2D_INPUT_COUNT 0

            #include "d2d1effecthelpers.hlsli"

            struct ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount
            {
                float Value;
                static ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount __ctor(float value);
                void __ctor__init(float value);
            };

            static float ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount_Read(ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount amount);

            static const float Scale = ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount_Read(ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount::__ctor(2.0));

            static ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount::__ctor(float value)
            {
                ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount __this = (ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount)0;
                __this.__ctor__init(value);
                return __this;
            }

            void ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount::__ctor__init(float value)
            {
                this.Value = value * 2;
            }

            static float ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount_Read(ComputeWeave_D2D1_Tests_ShaderRewriterTests_StaticFieldInitializerAmount amount)
            {
                return amount.Value;
            }

            D2D_PS_ENTRY(Execute)
            {
                return float4(Scale, 0, 0, 1);
            }
            """, shaderInfo.HlslSource);
    }

    /// <summary>
    /// The same import with the constructor written as an expression body, which used to end the generator
    /// with an exception. The two forms mean the same thing in C#, so the shader they produce is the same.
    /// </summary>
    [TestMethod]
    public void StaticFieldInitializerExpressionBodiedConstructor_IsRewrittenLikeABlockBodiedOne()
    {
        string blockBodied = D2D1ReflectionServices.GetShaderInfo<StaticFieldInitializerShader>().HlslSource;
        string expressionBodied = D2D1ReflectionServices.GetShaderInfo<StaticFieldInitializerFromAnArrowShader>().HlslSource;

        Assert.AreEqual(
            blockBodied.Replace("StaticFieldInitializerAmount", "StaticFieldInitializerAmountFromAnArrow"),
            expressionBodied);
    }

    /// <summary>
    /// A custom struct whose constructor computes rather than copies, so that the imported body shows.
    /// </summary>
    internal struct StaticFieldInitializerAmount
    {
        public float Value;

        public StaticFieldInitializerAmount(float value)
        {
            this.Value = value * 2;
        }

        public static float Read(StaticFieldInitializerAmount amount)
        {
            return amount.Value;
        }
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct StaticFieldInitializerShader : ID2D1PixelShader
    {
        private static readonly float Scale = StaticFieldInitializerAmount.Read(new StaticFieldInitializerAmount(2.0f));

        public float4 Execute()
        {
            return new float4(Scale, 0, 0, 1);
        }
    }

    /// <summary>
    /// The same struct with the constructor written as an expression body. The name extends the other one,
    /// so the shader it produces is the other shader with that one identifier rewritten.
    /// </summary>
    internal struct StaticFieldInitializerAmountFromAnArrow
    {
        public float Value;

        public StaticFieldInitializerAmountFromAnArrow(float value) => this.Value = value * 2;

        public static float Read(StaticFieldInitializerAmountFromAnArrow amount)
        {
            return amount.Value;
        }
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct StaticFieldInitializerFromAnArrowShader : ID2D1PixelShader
    {
        private static readonly float Scale = StaticFieldInitializerAmountFromAnArrow.Read(new StaticFieldInitializerAmountFromAnArrow(2.0f));

        public float4 Execute()
        {
            return new float4(Scale, 0, 0, 1);
        }
    }
}
