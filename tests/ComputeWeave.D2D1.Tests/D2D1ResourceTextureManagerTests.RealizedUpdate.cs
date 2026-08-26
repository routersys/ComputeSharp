using System;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp.PixelFormats;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace ComputeWeave.D2D1.Tests;

public partial class D2D1ResourceTextureManagerTests
{
    /// <summary>
    /// Updates a resource texture that has already been realized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other update tests call <c>Update</c> before the manager is attached to an effect, so the
    /// data goes into the staging buffer and <c>ID2D1ResourceTexture::Update</c> is never called. This
    /// test draws first, which realizes the texture, and only then updates, which takes the other
    /// branch of <c>D2D1ResourceTextureManagerImpl.Update</c> and dispatches through the vtable.
    /// </para>
    /// <para>
    /// Shifting that vtable index by one is caught here with an access violation. Before this test
    /// existed the only thing that reached the same call was
    /// <c>UpdateResourceTexture2D_ConcurrentAccess_DoesNotDeadlock</c>, which is ignored on
    /// integrated graphics, so on such a machine the mutation went entirely undetected.
    /// </para>
    /// </remarks>
    [TestMethod]
    public unsafe void UpdateResourceTexture2D_AfterRealization_DispatchesToTheTexture()
    {
        const int width = 64;
        const int height = 64;

        using ComPtr<ID2D1Factory2> d2D1Factory2 = D2D1Helper.CreateD2D1Factory2();
        using ComPtr<ID2D1Device> d2D1Device = D2D1Helper.CreateD2D1Device(d2D1Factory2.Get());
        using ComPtr<ID2D1DeviceContext> d2D1DeviceContext = D2D1Helper.CreateD2D1DeviceContext(d2D1Device.Get());

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<CopyFromResourceTexture2DShader>(d2D1Factory2.Get(), out _);

        using ComPtr<ID2D1Effect> d2D1Effect = default;

        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<CopyFromResourceTexture2DShader>(d2D1DeviceContext.Get(), (void**)d2D1Effect.GetAddressOf());

        CopyFromResourceTexture2DShader shader = new();

        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(d2D1Effect.Get(), in shader);

        byte[] texture = new byte[width * height];

        D2D1ResourceTextureManager resourceTextureManager = new(
            extents: [width, height],
            bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
            channelDepth: D2D1ChannelDepth.One,
            filter: D2D1Filter.MinMagMipPoint,
            extendModes: [D2D1ExtendMode.Clamp, D2D1ExtendMode.Clamp],
            data: texture,
            strides: [width]);

        D2D1PixelShaderEffect.SetResourceTextureManagerForD2D1Effect(d2D1Effect.Get(), resourceTextureManager, 0);

        using ComPtr<ID2D1Bitmap> d2D1BitmapTarget = D2D1Helper.CreateD2D1BitmapAndSetAsTarget(d2D1DeviceContext.Get(), width, height);

        // Realizes the resource texture from the staging buffer
        D2D1Helper.DrawEffect(d2D1DeviceContext.Get(), d2D1Effect.Get());

        byte[] data = new byte[width * height];

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((i * 7) + 1);
        }

        // Now that the texture exists, this dispatches through ID2D1ResourceTexture::Update
        resourceTextureManager.Update(
            minimumExtents: [0, 0],
            maximimumExtents: [width, height],
            strides: [width],
            data: data);

        D2D1Helper.DrawEffect(d2D1DeviceContext.Get(), d2D1Effect.Get());

        using ComPtr<ID2D1Bitmap1> d2D1Bitmap1Buffer = D2D1Helper.CreateD2D1Bitmap1Buffer(d2D1DeviceContext.Get(), d2D1BitmapTarget.Get(), out D2D1_MAPPED_RECT d2D1MappedRect);

        byte[] resultingBytes = new byte[width * height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            foreach (Bgra32 pixel in new ReadOnlySpan<Bgra32>(d2D1MappedRect.bits + (d2D1MappedRect.pitch * y), width))
            {
                resultingBytes[index++] = pixel.B;
            }
        }

        Assert.IsTrue(data.AsSpan().SequenceEqual(resultingBytes), "the second draw did not see the updated data");
    }
}
