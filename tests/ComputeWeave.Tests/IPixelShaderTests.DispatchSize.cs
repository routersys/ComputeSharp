using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

/// <summary>
/// Tests the dispatch size a pixel-shader-like shader is given on its third axis.
/// </summary>
/// <remarks>
/// A pixel-shader-like shader only ever runs over a two dimensional texture, so the constant buffer
/// carries no field for the third axis and the rewriter folds every read of it into the literal one.
/// The folding is invisible in the range check, which has no third comparison, so it is only the
/// value a shader reads back that says whether the right constant was folded in.
/// </remarks>
public partial class IPixelShaderTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void PixelShader_DispatchSizeThirdAxisIsOne(Device device)
    {
        using ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(16, 8);

        device.Get().ForEach<DispatchSizeThirdAxisShader, float4>(texture);

        Rgba32[,] result = texture.ToArray();

        // The three axes are written as a colour so they can be read back through the texture
        Assert.AreEqual(new Rgba32(16, 8, 1, 255), result[0, 0]);
        Assert.AreEqual(new Rgba32(16, 8, 1, 255), result[7, 15]);
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchSizeThirdAxisShader : IComputeShader<float4>
    {
        public float4 Execute()
        {
            return new float4(DispatchSize.X / 255f, DispatchSize.Y / 255f, DispatchSize.Z / 255f, 1);
        }
    }
}
