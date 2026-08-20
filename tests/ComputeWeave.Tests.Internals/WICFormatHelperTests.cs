using System;
using ComputeWeave.Graphics.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the container format every accepted file extension resolves to.
/// </summary>
/// <remarks>
/// The expected values are written here as literals, taken from the <c>GUID_ContainerFormat</c> definitions
/// of <c>wincodec.h</c>. Reading them back out of the implementation would assert nothing.
/// </remarks>
[TestClass]
public class WICFormatHelperTests
{
    /// <summary>
    /// The container format each extension the implementation lists must resolve to.
    /// </summary>
    private static readonly (string Extension, string ContainerFormat)[] Containers =
    [
        (".bmp", "0af1d87e-fcfe-4188-bdeb-a7906471cbe3"),
        (".dib", "0af1d87e-fcfe-4188-bdeb-a7906471cbe3"),
        (".rle", "0af1d87e-fcfe-4188-bdeb-a7906471cbe3"),
        (".png", "1b7cfaf4-713f-473c-bbcd-6137425faeaf"),
        (".jpg", "19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
        (".jpe", "19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
        (".jpeg", "19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
        (".jfif", "19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
        (".exif", "19e4a5aa-5662-4fc5-a0c0-1758028e1057"),
        (".wmp", "57a37caa-367a-4540-916b-f183c5093a4b"),
        (".jxr", "57a37caa-367a-4540-916b-f183c5093a4b"),
        (".hdp", "57a37caa-367a-4540-916b-f183c5093a4b"),
        (".wdp", "57a37caa-367a-4540-916b-f183c5093a4b"),
        (".tif", "163bcc30-e2e9-4f0b-961d-a3e9fdb788a3"),
        (".tiff", "163bcc30-e2e9-4f0b-961d-a3e9fdb788a3"),
        (".dds", "9967cb95-2e85-4ac8-8ca2-83d7ccd425c9")
    ];

    [TestMethod]
    public void ResolvesEveryExtensionItAccepts()
    {
        foreach ((string extension, string containerFormat) in Containers)
        {
            Guid actual = WICFormatHelper.GetForFilename(("image" + extension).AsSpan());

            Assert.AreEqual(new Guid(containerFormat), actual, extension);
        }
    }

    [TestMethod]
    public void ResolvesEveryExtensionRegardlessOfCase()
    {
        foreach ((string extension, string containerFormat) in Containers)
        {
            Guid actual = WICFormatHelper.GetForFilename(("IMAGE" + extension.ToUpperInvariant()).AsSpan());

            Assert.AreEqual(new Guid(containerFormat), actual, extension);
        }
    }

    [TestMethod]
    public void RefusesAnExtensionItDoesNotList()
    {
        foreach (string filename in new[] { "image.gif", "image.webp", "image.heif", "image", "image." })
        {
            _ = Assert.ThrowsExactly<ArgumentException>(() => _ = WICFormatHelper.GetForFilename(filename.AsSpan()), filename);
        }
    }
}
