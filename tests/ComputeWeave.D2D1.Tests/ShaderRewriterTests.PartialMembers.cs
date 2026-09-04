using ComputeWeave.D2D1.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// What a declaration split into a defining and an implementing part is written as on the Direct2D path.
/// </summary>
/// <remarks>
/// The declaration is read from a symbol at one place, which both generators share, but the entry point is
/// looked up by each of them on its own. The compute side covers its own entry point, so this pins the other
/// one. The shaders here are compiled through FXC, so a body that failed to be written is answered for here.
/// </remarks>
public partial class ShaderRewriterTests
{
    [TestMethod]
    public void PartialEntryPoint_IsWrittenLikeAWholeOne()
    {
        string split = D2D1ReflectionServices.GetShaderInfo<PartialEntryPointShader>().HlslSource;
        string whole = D2D1ReflectionServices.GetShaderInfo<WholeEntryPointShader>().HlslSource;

        Assert.AreEqual(whole, split);
    }

    [TestMethod]
    public void PartialImportedMethod_IsWrittenLikeAWholeOne()
    {
        string split = D2D1ReflectionServices.GetShaderInfo<PartialImportedMethodShader>().HlslSource;
        string whole = D2D1ReflectionServices.GetShaderInfo<WholeImportedMethodShader>().HlslSource;

        Assert.AreEqual(
            whole.Replace("PartialMemberWholeHelper", "PartialMemberSplitHelper"),
            split);
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct PartialEntryPointShader : ID2D1PixelShader
    {
        public partial float4 Execute();
    }

    internal readonly partial struct PartialEntryPointShader
    {
        public partial float4 Execute()
        {
            return new float4(1, 0, 0, 1);
        }
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct WholeEntryPointShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return new float4(1, 0, 0, 1);
        }
    }

    /// <summary>
    /// A helper whose method is split. The name differs from the whole one so that the two shaders do not
    /// share a declaration, and the comparison rewrites that one identifier.
    /// </summary>
    internal static partial class PartialMemberSplitHelper
    {
        public static partial float Twice(float value);
    }

    internal static partial class PartialMemberSplitHelper
    {
        public static partial float Twice(float value)
        {
            return value * 2;
        }
    }

    internal static class PartialMemberWholeHelper
    {
        public static float Twice(float value)
        {
            return value * 2;
        }
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct PartialImportedMethodShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return new float4(PartialMemberSplitHelper.Twice(0.5f), 0, 0, 1);
        }
    }

    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct WholeImportedMethodShader : ID2D1PixelShader
    {
        public float4 Execute()
        {
            return new float4(PartialMemberWholeHelper.Twice(0.5f), 0, 0, 1);
        }
    }
}
