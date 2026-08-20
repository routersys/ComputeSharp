using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class TextureViewTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64, true)]
    [Data(100, false)]
    public unsafe void TryGetSpan_Texture2D_MatchesMappingContiguity(Device device, int width, bool isContiguous)
    {
        using UploadTexture2D<float> texture = device.Get().AllocateUploadTexture2D<float>(width, 4);

        _ = texture.View.DangerousGetAddressAndByteStride(out int strideInBytes);

        Assert.AreEqual(isContiguous, strideInBytes == width * sizeof(float));
        Assert.AreEqual(isContiguous, texture.View.TryGetSpan(out Span<float> span));

        if (isContiguous)
        {
            Assert.AreEqual(texture.View.Length, span.Length);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64, true)]
    [Data(100, false)]
    public unsafe void TryGetSpan_Texture3D_MatchesMappingContiguity(Device device, int width, bool isContiguous)
    {
        using UploadTexture3D<float> texture = device.Get().AllocateUploadTexture3D<float>(width, 4, 2);

        _ = texture.View.DangerousGetAddressAndByteStride(out int strideInBytes);

        Assert.AreEqual(isContiguous, strideInBytes == width * sizeof(float));
        Assert.AreEqual(isContiguous, texture.View.TryGetSpan(out Span<float> span));

        if (isContiguous)
        {
            Assert.AreEqual(texture.View.Length, span.Length);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture2D_LongerDestination_Ok(Device device, int width)
    {
        using UploadTexture2D<float> texture = device.Get().AllocateUploadTexture2D<float>(width, 4);

        Assert.IsTrue(texture.View.TryCopyTo(new float[texture.View.Length + 1]));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture3D_LongerDestination_Ok(Device device, int width)
    {
        using UploadTexture3D<float> texture = device.Get().AllocateUploadTexture3D<float>(width, 4, 2);

        Assert.IsTrue(texture.View.TryCopyTo(new float[texture.View.Length + 1]));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture2D_ShorterDestination_Fail(Device device, int width)
    {
        using UploadTexture2D<float> texture = device.Get().AllocateUploadTexture2D<float>(width, 4);

        float[] destination = new float[texture.View.Length - 1];

        Array.Fill(destination, float.NaN);

        Type? thrown = null;

        try
        {
            texture.View.CopyTo(destination);
        }
        catch (Exception e)
        {
            thrown = e.GetType();
        }

        Assert.AreEqual(typeof(ArgumentException), thrown);

        foreach (float value in destination)
        {
            Assert.IsTrue(float.IsNaN(value));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture3D_ShorterDestination_Fail(Device device, int width)
    {
        using UploadTexture3D<float> texture = device.Get().AllocateUploadTexture3D<float>(width, 4, 2);

        float[] destination = new float[texture.View.Length - 1];

        Array.Fill(destination, float.NaN);

        Type? thrown = null;

        try
        {
            texture.View.CopyTo(destination);
        }
        catch (Exception e)
        {
            thrown = e.GetType();
        }

        Assert.AreEqual(typeof(ArgumentException), thrown);

        foreach (float value in destination)
        {
            Assert.IsTrue(float.IsNaN(value));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture2D_Contents_Ok(Device device, int width)
    {
        using UploadTexture2D<float> texture = device.Get().AllocateUploadTexture2D<float>(width, 4);

        for (int y = 0; y < texture.Height; y++)
        {
            Span<float> row = texture.View.GetRowSpan(y);

            for (int x = 0; x < width; x++)
            {
                row[x] = (y * width) + x;
            }
        }

        float[,] items = texture.View.ToArray();

        Assert.AreEqual(texture.Height, items.GetLength(0));
        Assert.AreEqual(width, items.GetLength(1));

        for (int y = 0; y < texture.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.AreEqual((y * width) + x, items[y, x]);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(64)]
    [Data(100)]
    public void CopyTo_Texture3D_Contents_Ok(Device device, int width)
    {
        using UploadTexture3D<float> texture = device.Get().AllocateUploadTexture3D<float>(width, 4, 2);

        for (int z = 0; z < texture.Depth; z++)
        {
            for (int y = 0; y < texture.Height; y++)
            {
                Span<float> row = texture.View.GetRowSpan(y, z);

                for (int x = 0; x < width; x++)
                {
                    row[x] = (((z * texture.Height) + y) * width) + x;
                }
            }
        }

        float[,,] items = texture.View.ToArray();

        Assert.AreEqual(texture.Depth, items.GetLength(0));
        Assert.AreEqual(texture.Height, items.GetLength(1));
        Assert.AreEqual(width, items.GetLength(2));

        for (int z = 0; z < texture.Depth; z++)
        {
            for (int y = 0; y < texture.Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.AreEqual((((z * texture.Height) + y) * width) + x, items[z, y, x]);
                }
            }
        }
    }
}
