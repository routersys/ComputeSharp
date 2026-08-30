using System;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp.PixelFormats;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

#pragma warning disable CS0649, IDE0044

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests pinning the size a pixel shader reads back from a resource texture.
/// </summary>
/// <remarks>
/// Each of these accessors is written out as one output of a <c>GetDimensions</c> call, and which output it
/// takes is a number in the generator's mapping table. Reading the height out of the slot that holds the width
/// compiles and returns a plausible number, so only the value gives it away. Every resource texture here is
/// therefore given extents that differ from one another, both within a rank and across ranks: equal extents
/// would return the same number for two axes and hide exactly the mistake these tests are for.
/// </remarks>
[TestClass]
public partial class D2D1ResourceTextureDimensionsTests
{
    /// <summary>The extent of the one dimensional resource texture.</summary>
    private const int Width1D = 37;

    /// <summary>The extents of the two dimensional resource texture.</summary>
    private const int Width2D = 41;
    private const int Height2D = 23;

    /// <summary>The extents of the three dimensional resource texture.</summary>
    private const int Width3D = 53;
    private const int Height3D = 29;
    private const int Depth3D = 7;

    /// <summary>
    /// Reads back the first pixel an effect drew.
    /// </summary>
    /// <remarks>
    /// The shaders write each extent as that many 255ths, which a normalized 8 bit channel carries back
    /// exactly for a whole number below 256. Every pixel holds the same value, so one of them is enough.
    /// </remarks>
    private static unsafe Bgra32 DrawAndReadFirstPixel(ID2D1DeviceContext* d2D1DeviceContext, ID2D1Effect* d2D1Effect)
    {
        using ComPtr<ID2D1Bitmap> d2D1BitmapTarget = D2D1Helper.CreateD2D1BitmapAndSetAsTarget(d2D1DeviceContext, 4, 4);

        D2D1Helper.DrawEffect(d2D1DeviceContext, d2D1Effect);

        using ComPtr<ID2D1Bitmap1> d2D1Bitmap1Buffer = D2D1Helper.CreateD2D1Bitmap1Buffer(d2D1DeviceContext, d2D1BitmapTarget.Get(), out D2D1_MAPPED_RECT d2D1MappedRect);

        return new ReadOnlySpan<Bgra32>(d2D1MappedRect.bits, 1)[0];
    }

    [TestMethod]
    public unsafe void ResourceTexture1D_Width()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ResourceTexture1DDimensionsShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ResourceTexture1DDimensionsShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(d2D1Effect.Get(), default(ResourceTexture1DDimensionsShader));

        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [Width1D],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp],
            data: new byte[Width1D],
            strides: null);

        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 0);

        Bgra32 pixel = DrawAndReadFirstPixel(d2D1DeviceContext.Get(), d2D1Effect.Get());

        Assert.AreEqual(Width1D, pixel.R);
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ResourceTexture1DDimensionsShader : ID2D1PixelShader
    {
        [D2DResourceTextureIndex(0)]
        private readonly D2D1ResourceTexture1D<float> source;

        /// <inheritdoc/>
        public float4 Execute()
        {
            return new float4(this.source.Width / 255.0f, 0, 0, 1);
        }
    }

    [TestMethod]
    public unsafe void ResourceTexture2D_WidthAndHeight()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ResourceTexture2DDimensionsShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ResourceTexture2DDimensionsShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(d2D1Effect.Get(), default(ResourceTexture2DDimensionsShader));

        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [Width2D, Height2D],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp, D2D1ExtendMode.Clamp],
            data: new byte[Width2D * Height2D],
            strides: [Width2D]);

        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 0);

        Bgra32 pixel = DrawAndReadFirstPixel(d2D1DeviceContext.Get(), d2D1Effect.Get());

        Assert.AreEqual(Width2D, pixel.R);
        Assert.AreEqual(Height2D, pixel.G);
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ResourceTexture2DDimensionsShader : ID2D1PixelShader
    {
        [D2DResourceTextureIndex(0)]
        private readonly D2D1ResourceTexture2D<float> source;

        /// <inheritdoc/>
        public float4 Execute()
        {
            return new float4(this.source.Width / 255.0f, this.source.Height / 255.0f, 0, 1);
        }
    }

    [TestMethod]
    public unsafe void ResourceTexture3D_WidthAndHeightAndDepth()
    {
        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<ResourceTexture3DDimensionsShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<ResourceTexture3DDimensionsShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(d2D1Effect.Get(), default(ResourceTexture3DDimensionsShader));

        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [Width3D, Height3D, Depth3D],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp, D2D1ExtendMode.Clamp, D2D1ExtendMode.Clamp],
            data: new byte[Width3D * Height3D * Depth3D],
            strides: [Width3D, Width3D * Height3D]);

        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 0);

        Bgra32 pixel = DrawAndReadFirstPixel(d2D1DeviceContext.Get(), d2D1Effect.Get());

        Assert.AreEqual(Width3D, pixel.R);
        Assert.AreEqual(Height3D, pixel.G);
        Assert.AreEqual(Depth3D, pixel.B);
    }

    [D2DInputCount(0)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ResourceTexture3DDimensionsShader : ID2D1PixelShader
    {
        [D2DResourceTextureIndex(0)]
        private readonly D2D1ResourceTexture3D<float> source;

        /// <inheritdoc/>
        public float4 Execute()
        {
            return new float4(
                this.source.Width / 255.0f,
                this.source.Height / 255.0f,
                this.source.Depth / 255.0f,
                1);
        }
    }
}
