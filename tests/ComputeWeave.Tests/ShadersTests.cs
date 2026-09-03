using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ComputeWeave.Descriptors;
using ComputeWeave.Interop;
using ComputeWeave.SwapChain.Shaders;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpRgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace ComputeWeave.Tests;

[TestClass]
public class ShadersTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(HelloWorld))]
    [Data(typeof(SwapChain.Shaders.Compute.HelloWorld))]
    public void HelloWorld(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0000004f);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(ColorfulInfinity))]
    [Data(typeof(SwapChain.Shaders.Compute.ColorfulInfinity))]
    public void ColorfulInfinity(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0000004f);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(FourColorGradient))]
    [Data(typeof(SwapChain.Shaders.Compute.FourColorGradient))]
    public void FourColorGradient(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.00021f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(FractalTiling))]
    [Data(typeof(SwapChain.Shaders.Compute.FractalTiling))]
    public void FractalTiling(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0000005f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(MengerJourney))]
    [Data(typeof(SwapChain.Shaders.Compute.MengerJourney))]
    public void MengerJourney(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.000011f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(TwoTiledTruchet))]
    [Data(typeof(SwapChain.Shaders.Compute.TwoTiledTruchet))]
    public void TwoTiledTruchet(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.00032f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(Octagrams))]
    [Data(typeof(SwapChain.Shaders.Compute.Octagrams))]
    public void Octagrams(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0000006f);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(ProteanClouds))]
    [Data(typeof(SwapChain.Shaders.Compute.ProteanClouds))]
    public void ProteanClouds(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0000004f);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(ExtrudedTruchetPattern))]
    [Data(typeof(SwapChain.Shaders.Compute.ExtrudedTruchetPattern))]
    public void ExtrudedTruchetPattern(Device device, Type shaderType)
    {
        ShaderInfo info = ReflectionServices.GetShaderInfo<SwapChain.Shaders.Compute.ExtrudedTruchetPattern>();

        Assert.IsFalse(info.RequiresDoublePrecisionSupport);

        RunAndCompareShader(device, shaderType, 0.00011f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(PyramidPattern))]
    [Data(typeof(SwapChain.Shaders.Compute.PyramidPattern))]
    public void PyramidPattern(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.00021f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(TriangleGridContouring))]
    [Data(typeof(SwapChain.Shaders.Compute.TriangleGridContouring))]
    public void TriangleGridContouring(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.0006f, usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(ContouredLayers))]
    [Data(typeof(SwapChain.Shaders.Compute.ContouredLayers))]
    public void ContouredLayers(Device device, Type shaderType)
    {
        string filename = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Shaders", "Textures", "RustyMetal.png");

        using ReadOnlyTexture2D<Rgba32, float4> background = device.Get().LoadReadOnlyTexture2D<Rgba32, float4>(filename);

        RunAndCompareShader(
            device,
            shaderType,
            texture => new SwapChain.Shaders.Compute.ContouredLayers(texture, 0, background),
            texture => new ContouredLayers(0, background),
            0.000703f,
            usesTranscendentalHash: true);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(typeof(TerracedHills))]
    [Data(typeof(SwapChain.Shaders.Compute.TerracedHills))]
    public void TerracedHills(Device device, Type shaderType)
    {
        RunAndCompareShader(device, shaderType, 0.000026f, usesTranscendentalHash: true);
    }

    /// <summary>
    /// Executes a given test for a specified shader.
    /// </summary>
    /// <param name="device">The device to use.</param>
    /// <param name="shaderType">The type of shader being executed.</param>
    /// <param name="delta">The comparison delta.</param>
    /// <param name="usesTranscendentalHash">Whether the shader scales <c>sin</c> or <c>cos</c> past its low bits and takes the fraction, which ties the image it draws to the implementation of that function on the device.</param>
    private static void RunAndCompareShader(Device device, Type shaderType, float delta, bool usesTranscendentalHash = false)
    {
        _ = device.Get();

        string expectedPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Assets", $"{shaderType.Name}.png");

        ImageInfo imageInfo = Image.Identify(expectedPath);

        using Image<ImageSharpRgba32> image = new(imageInfo.Width, imageInfo.Height);

        using (ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(imageInfo.Width, imageInfo.Height))
        {
            if (typeof(IComputeShader).IsAssignableFrom(shaderType))
            {
                static void RunComputeShader<T>(ReadWriteTexture2D<Rgba32, float4> texture)
                    where T : struct, IComputeShader, IComputeShaderDescriptor<T>
                {
                    ShaderInfo info = ReflectionServices.GetShaderInfo<T>();

                    Assert.IsFalse(info.RequiresDoublePrecisionSupport);

                    texture.GraphicsDevice.For(texture.Width, texture.Height, (T)Activator.CreateInstance(typeof(T), texture, 0f)!);
                }

                Action<ReadWriteTexture2D<Rgba32, float4>> action = new(RunComputeShader<SwapChain.Shaders.Compute.ColorfulInfinity>);

                _ = action.Method.GetGenericMethodDefinition().MakeGenericMethod(shaderType).Invoke(null, [texture]);
            }
            else
            {
                static void RunPixelShader<T>(ReadWriteTexture2D<Rgba32, float4> texture)
                    where T : struct, IComputeShader<float4>, IComputeShaderDescriptor<T>
                {
                    ShaderInfo info = ReflectionServices.GetShaderInfo<T, float4>();

                    Assert.IsFalse(info.RequiresDoublePrecisionSupport);

                    texture.GraphicsDevice.ForEach(texture, (T)Activator.CreateInstance(typeof(T), 0f)!);
                }

                Action<ReadWriteTexture2D<Rgba32, float4>> action = new(RunPixelShader<ColorfulInfinity>);

                _ = action.Method.GetGenericMethodDefinition().MakeGenericMethod(shaderType).Invoke(null, [texture]);
            }

            _ = image.DangerousTryGetSinglePixelMemory(out Memory<ImageSharpRgba32> memory);

            texture.CopyTo(MemoryMarshal.Cast<ImageSharpRgba32, Rgba32>(memory.Span));
        }

        string actualPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Shaders", $"{shaderType.Name}.png");

        _ = Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);

        image.SaveAsPng(actualPath, new PngEncoder() { CompressionLevel = PngCompressionLevel.BestCompression, ColorType = PngColorType.Rgb });

        // Only the WARP variants are held to the reference images, so a hash of sin or cos compares there
        if (usesTranscendentalHash && device != Device.Warp)
        {
            Assert.Inconclusive(
                $"{shaderType.Name} folds sin or cos into a hash, so its image follows the implementation of that function rather than anything the library emits. Only the WARP variant is held to the reference image, so a run on {device} does not decide whether the image is right.");
        }

        TolerantImageComparer.AssertEqual(expectedPath, actualPath, delta);
    }

    /// <summary>
    /// Executes a given test for a specified shader.
    /// </summary>
    /// <typeparam name="TCompute">The type of compute shader to test.</typeparam>
    /// <typeparam name="TPixel">The type of pixel shader to test.</typeparam>
    /// <param name="device">The device to use.</param>
    /// <param name="shaderType">The type of shader being executed.</param>
    /// <param name="computeFactory">The factory of compute shaders.</param>
    /// <param name="pixelFactory">The factory of pixel shaders.</param>
    /// <param name="delta">The comparison delta.</param>
    /// <param name="usesTranscendentalHash">Whether the shader scales <c>sin</c> or <c>cos</c> past its low bits and takes the fraction, which ties the image it draws to the implementation of that function on the device.</param>
    private static void RunAndCompareShader<TCompute, TPixel>(
        Device device,
        Type shaderType,
        Func<ReadWriteTexture2D<Rgba32, float4>, TCompute> computeFactory,
        Func<ReadWriteTexture2D<Rgba32, float4>, TPixel> pixelFactory,
        float delta,
        bool usesTranscendentalHash = false)
        where TCompute : struct, IComputeShader, IComputeShaderDescriptor<TCompute>
        where TPixel : struct, IComputeShader<float4>, IComputeShaderDescriptor<TPixel>
    {
        _ = device.Get();

        string expectedPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Assets", $"{shaderType.Name}.png");

        ImageInfo imageInfo = Image.Identify(expectedPath);

        using Image<ImageSharpRgba32> image = new(imageInfo.Width, imageInfo.Height);

        using (ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(imageInfo.Width, imageInfo.Height))
        {
            if (typeof(IComputeShader).IsAssignableFrom(shaderType))
            {
                ShaderInfo info = ReflectionServices.GetShaderInfo<TCompute>();

                Assert.IsFalse(info.RequiresDoublePrecisionSupport);

                Assert.AreEqual(typeof(TCompute), shaderType);

                texture.GraphicsDevice.For(texture.Width, texture.Height, computeFactory(texture));
            }
            else
            {
                Assert.AreEqual(typeof(TPixel), shaderType);

                texture.GraphicsDevice.ForEach(texture, pixelFactory(texture));
            }

            _ = image.DangerousTryGetSinglePixelMemory(out Memory<ImageSharpRgba32> memory);

            texture.CopyTo(MemoryMarshal.Cast<ImageSharpRgba32, Rgba32>(memory.Span));
        }

        string actualPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Shaders", $"{shaderType.Name}.png");

        _ = Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);

        image.SaveAsPng(actualPath, new PngEncoder() { CompressionLevel = PngCompressionLevel.BestCompression, ColorType = PngColorType.Rgb });

        // Only the WARP variants are held to the reference images, so a hash of sin or cos compares there
        if (usesTranscendentalHash && device != Device.Warp)
        {
            Assert.Inconclusive(
                $"{shaderType.Name} folds sin or cos into a hash, so its image follows the implementation of that function rather than anything the library emits. Only the WARP variant is held to the reference image, so a run on {device} does not decide whether the image is right.");
        }

        TolerantImageComparer.AssertEqual(expectedPath, actualPath, delta);
    }
}