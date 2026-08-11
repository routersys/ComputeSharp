using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComputeWeave.SourceGenerators.Dxc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Dxc;

[TestClass]
public class DxcLibraryLoaderTests
{
    [TestMethod]
    public void CacheKeySeparatesResourceBoundaries()
    {
        using MemoryStream dxilA = new([1]);
        using MemoryStream dxcompilerA = new([2, 3]);
        using MemoryStream dxilB = new([1, 2]);
        using MemoryStream dxcompilerB = new([3]);

        string keyA = DxcLibraryLoader.GetCacheKey(dxilA, dxcompilerA);
        string keyB = DxcLibraryLoader.GetCacheKey(dxilB, dxcompilerB);

        Assert.AreEqual(64, keyA.Length);
        Assert.AreNotEqual(keyA, keyB);
    }

    [TestMethod]
    public void ExistingLibraryIsNotOverwritten()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");

        try
        {
            File.WriteAllBytes(targetFilename, [1, 2, 3]);

            using MemoryStream sourceStream = new([4, 5, 6]);

            DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, targetFilename);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(targetFilename));
            Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void ConcurrentExtractionPublishesOneCompleteLibrary()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");
        byte[] expected = Enumerable.Range(0, 65536).Select(static value => (byte)value).ToArray();
        using ManualResetEventSlim start = new();
        List<Task> tasks = [];

        try
        {
            for (int i = 0; i < 16; i++)
            {
                tasks.Add(Task.Factory.StartNew(
                    () =>
                    {
                        start.Wait();

                        using MemoryStream sourceStream = new(expected, false);

                        DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, targetFilename);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default));
            }

            start.Set();
            Task.WaitAll([.. tasks]);

            CollectionAssert.AreEqual(expected, File.ReadAllBytes(targetFilename));
            Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void FailedExtractionLeavesNoFile()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");

        try
        {
            using FailingStream sourceStream = new();

            _ = Assert.ThrowsException<IOException>(() => DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, targetFilename));

            Assert.IsFalse(File.Exists(targetFilename));
            Assert.AreEqual(0, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static string CreateTemporaryFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), nameof(DxcLibraryLoaderTests), Path.GetRandomFileName());

        _ = Directory.CreateDirectory(folder);

        return folder;
    }

    private sealed class FailingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException();
        }

        public override int Read(Span<byte> buffer)
        {
            throw new IOException();
        }
    }
}
