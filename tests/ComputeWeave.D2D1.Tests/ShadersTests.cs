using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ComputeWeave.D2D1.Descriptors;
using ComputeWeave.D2D1.Interop;
using ComputeWeave.D2D1.Tests.Helpers;
using ComputeWeave.SwapChain.Shaders.D2D1;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ComputeWeave.D2D1.Tests;

[TestClass]
public class ShadersTests
{
    [TestMethod]
    public void HelloWorld()
    {
        RunTest<HelloWorld>();
    }

    [TestMethod]
    public void ColorfulInfinity()
    {
        RunTest<ColorfulInfinity>();
    }

    [TestMethod]
    public void FractalTiling()
    {
        RunTest<FractalTiling>(usesTranscendentalHash: true);
    }

    [TestMethod]
    public void MengerJourney()
    {
        RunTest<MengerJourney>(0.000011f, usesTranscendentalHash: true);
    }

    [TestMethod]
    public void TwoTiledTruchet()
    {
        RunTest<TwoTiledTruchet>(usesTranscendentalHash: true);
    }

    [TestMethod]
    public void Octagrams()
    {
        RunTest<Octagrams>();
    }

    [TestMethod]
    public void ProteanClouds()
    {
        RunTest<ProteanClouds>();
    }

    [TestMethod]
    public void PyramidPattern()
    {
        RunTest<PyramidPattern>(usesTranscendentalHash: true);
    }

    [TestMethod]
    public void TriangleGridContouring()
    {
        RunTest<TriangleGridContouring>(usesTranscendentalHash: true);
    }

    [TestMethod]
    public unsafe void ContouredLayers()
    {
        string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string expectedPath = Path.Combine(assemblyPath, "Assets", "Textures", "RustyMetal.png");

        D2D1ResourceTextureManager resourceTextureManager;

        using (Image<Rgba32> texture = Image.Load<Rgba32>(expectedPath))
        {
            if (!texture.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> pixels))
            {
                Assert.Inconclusive();
            }

            resourceTextureManager = new D2D1ResourceTextureManager(
                extents: [(uint)texture.Width, (uint)texture.Height],
                bufferPrecision: D2D1BufferPrecision.UInt8Normalized,
                channelDepth: D2D1ChannelDepth.Four,
                filter: D2D1Filter.MinMagMipLinear,
                extendModes: [D2D1ExtendMode.Mirror, D2D1ExtendMode.Mirror],
                data: MemoryMarshal.AsBytes(pixels.Span),
                strides: [(uint)(texture.Width * sizeof(Rgba32))]);
        }

        ContouredLayers shader = new(0f, new int2(1280, 720));

        D2D1TestRunner.RunAndCompareShader(in shader, 1280, 720, $"{nameof(ContouredLayers)}.png", nameof(ContouredLayers), 0.0002f, usesTranscendentalHash: true, resourceTextures: (0, resourceTextureManager));
    }

    [TestMethod]
    public void TerracedHills()
    {
        RunTest<TerracedHills>(0.000027f, usesTranscendentalHash: true);
    }

    private static void RunTest<T>(float threshold = 0.00001f, bool usesTranscendentalHash = false)
        where T : unmanaged, ID2D1PixelShader, ID2D1PixelShaderDescriptor<T>
    {
        T shader = (T)Activator.CreateInstance(typeof(T), 0f, new int2(1280, 720))!;

        D2D1TestRunner.RunAndCompareShader(in shader, 1280, 720, $"{typeof(T).Name}.png", typeof(T).Name, threshold, usesTranscendentalHash);
    }
}