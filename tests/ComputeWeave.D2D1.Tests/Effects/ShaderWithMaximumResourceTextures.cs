namespace ComputeWeave.D2D1.Tests.Effects;

// Declares as many resource textures as there are ResourceTextureManager properties
[D2DInputCount(0)]
[D2DGeneratedPixelShaderDescriptor]
[AutoConstructor]
internal readonly partial struct ShaderWithMaximumResourceTextures : ID2D1PixelShader
{
    [D2DResourceTextureIndex(0)]
    public readonly D2D1ResourceTexture1D<float> t0;

    [D2DResourceTextureIndex(1)]
    public readonly D2D1ResourceTexture1D<float> t1;

    [D2DResourceTextureIndex(2)]
    public readonly D2D1ResourceTexture1D<float> t2;

    [D2DResourceTextureIndex(3)]
    public readonly D2D1ResourceTexture1D<float> t3;

    [D2DResourceTextureIndex(4)]
    public readonly D2D1ResourceTexture1D<float> t4;

    [D2DResourceTextureIndex(5)]
    public readonly D2D1ResourceTexture1D<float> t5;

    [D2DResourceTextureIndex(6)]
    public readonly D2D1ResourceTexture1D<float> t6;

    [D2DResourceTextureIndex(7)]
    public readonly D2D1ResourceTexture1D<float> t7;

    [D2DResourceTextureIndex(8)]
    public readonly D2D1ResourceTexture1D<float> t8;

    [D2DResourceTextureIndex(9)]
    public readonly D2D1ResourceTexture1D<float> t9;

    [D2DResourceTextureIndex(10)]
    public readonly D2D1ResourceTexture1D<float> t10;

    [D2DResourceTextureIndex(11)]
    public readonly D2D1ResourceTexture1D<float> t11;

    [D2DResourceTextureIndex(12)]
    public readonly D2D1ResourceTexture1D<float> t12;

    [D2DResourceTextureIndex(13)]
    public readonly D2D1ResourceTexture1D<float> t13;

    [D2DResourceTextureIndex(14)]
    public readonly D2D1ResourceTexture1D<float> t14;

    [D2DResourceTextureIndex(15)]
    public readonly D2D1ResourceTexture1D<float> t15;

    public float4 Execute()
    {
        return 0;
    }
}
