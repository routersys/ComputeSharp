using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ComputeWeave.SourceGenerators.Dxc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Dxc;

[TestClass]
public class DxcLibraryLoaderTests
{
    [TestMethod]
    public void EmbeddedDxcLibrariesCanBeLoaded()
    {
        DxcLibraryLoader.LoadNativeDxcLibraries();
    }

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
    public void ExistingVerifiedLibraryIsNotOverwritten()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");
        byte[] expected = [1, 2, 3];
        DateTime timestamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        try
        {
            File.WriteAllBytes(targetFilename, expected);
            File.SetLastWriteTimeUtc(targetFilename, timestamp);

            using MemoryStream sourceStream = new(expected);
            using FileStream library = DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash(expected), targetFilename);

            CollectionAssert.AreEqual(expected, File.ReadAllBytes(targetFilename));
            Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(targetFilename));
            Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void CorruptExistingLibrariesAreReplaced()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");
        byte[] expected = [1, 2, 3];
        byte[][] corruptLibraries = [[], [1], [1, 2, 3, 4], [3, 2, 1]];

        try
        {
            foreach (byte[] corruptLibrary in corruptLibraries)
            {
                File.WriteAllBytes(targetFilename, corruptLibrary);

                using MemoryStream sourceStream = new(expected);
                using FileStream library = DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash(expected), targetFilename);

                CollectionAssert.AreEqual(expected, File.ReadAllBytes(targetFilename));
                Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void ConcurrentCorruptReaderDoesNotPreventRepair()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");
        byte[] expected = [1, 2, 3];
        using ManualResetEventSlim readerOpened = new();
        using ManualResetEventSlim releaseReader = new();

        try
        {
            File.WriteAllBytes(targetFilename, [3, 2, 1]);

            Task reader = Task.Factory.StartNew(
                () =>
                {
                    using FileStream stream = new(targetFilename, FileMode.Open, FileAccess.Read, FileShare.Read);

                    readerOpened.Set();
                    releaseReader.Wait();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            readerOpened.Wait();

            Task<FileStream> repair = Task.Factory.StartNew(
                () =>
                {
                    using MemoryStream sourceStream = new(expected, false);

                    return DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash(expected), targetFilename);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            try
            {
                Assert.IsTrue(SpinWait.SpinUntil(
                    () => repair.IsCompleted || Directory.EnumerateFiles(folder).Count() > 1,
                    TimeSpan.FromSeconds(5)));
                Assert.IsFalse(repair.Wait(TimeSpan.FromMilliseconds(100)));

                releaseReader.Set();

                using FileStream library = repair.GetAwaiter().GetResult();

                reader.GetAwaiter().GetResult();
                CollectionAssert.AreEqual(expected, ReadAllBytes(library));
                CollectionAssert.AreEqual(expected, File.ReadAllBytes(targetFilename));
                Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());
            }
            finally
            {
                releaseReader.Set();
                _ = SpinWait.SpinUntil(() => repair.IsCompleted, TimeSpan.FromSeconds(5));

                if (repair.Status is TaskStatus.RanToCompletion)
                {
                    repair.Result.Dispose();
                }

                reader.GetAwaiter().GetResult();
            }
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
        byte[] expectedHash = GetHash(expected);
        List<Task<FileStream>> tasks = [];

        try
        {
            for (int i = 0; i < 16; i++)
            {
                tasks.Add(Task.Factory.StartNew(
                    () =>
                    {
                        start.Wait();

                        using MemoryStream sourceStream = new(expected, false);

                        return DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, expectedHash, targetFilename);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default));
            }

            try
            {
                start.Set();
                Task.WaitAll([.. tasks]);

                CollectionAssert.AreEqual(expected, File.ReadAllBytes(targetFilename));
                Assert.AreEqual(1, Directory.EnumerateFiles(folder).Count());

                foreach (Task<FileStream> task in tasks)
                {
                    CollectionAssert.AreEqual(expected, ReadAllBytes(task.Result));
                }
            }
            finally
            {
                foreach (Task<FileStream> task in tasks)
                {
                    if (task.Status is TaskStatus.RanToCompletion)
                    {
                        task.Result.Dispose();
                    }
                }
            }
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

            _ = Assert.ThrowsException<IOException>(() => DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash([1]), targetFilename));

            Assert.IsFalse(File.Exists(targetFilename));
            Assert.AreEqual(0, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void MismatchedExtractionIsNotPublished()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");

        try
        {
            using MemoryStream sourceStream = new([1, 2, 3]);

            _ = Assert.ThrowsException<InvalidDataException>(() => DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash([4, 5, 6]), targetFilename));

            Assert.IsFalse(File.Exists(targetFilename));
            Assert.AreEqual(0, Directory.EnumerateFiles(folder).Count());
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void VerifiedLibraryHandlePreventsModification()
    {
        string folder = CreateTemporaryFolder();
        string targetFilename = Path.Combine(folder, "dxcompiler.dll");
        byte[] expected = [1, 2, 3];

        try
        {
            using MemoryStream sourceStream = new(expected);
            FileStream library = DxcLibraryLoader.ExtractLibraryAtomically(sourceStream, GetHash(expected), targetFilename);

            try
            {
                _ = Assert.ThrowsException<IOException>(() => File.Open(targetFilename, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
                _ = Assert.ThrowsException<IOException>(() => File.Delete(targetFilename));
            }
            finally
            {
                library.Dispose();
            }

            File.WriteAllBytes(targetFilename, [4, 5, 6]);

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, File.ReadAllBytes(targetFilename));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void CacheFolderRequiresRootedLocalApplicationData()
    {
        _ = Assert.ThrowsException<InvalidOperationException>(() => DxcLibraryLoader.GetCacheFolder(string.Empty, "key"));
        _ = Assert.ThrowsException<InvalidOperationException>(() => DxcLibraryLoader.GetCacheFolder("relative", "key"));

        string localApplicationData = Path.GetPathRoot(Path.GetTempPath())!;
        string folder = DxcLibraryLoader.GetCacheFolder(localApplicationData, "key");

        Assert.AreEqual(Path.Combine(localApplicationData, "ComputeWeave", "SourceGenerators", "Dxc", "key"), folder);
    }

    private static string CreateTemporaryFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), nameof(DxcLibraryLoaderTests), Path.GetRandomFileName());

        _ = Directory.CreateDirectory(folder);

        return folder;
    }

    private static byte[] GetHash(byte[] bytes)
    {
        using SHA256 hashAlgorithm = SHA256.Create();

        return hashAlgorithm.ComputeHash(bytes);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        stream.Position = 0;

        using MemoryStream destination = new();

        stream.CopyTo(destination);

        return destination.ToArray();
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
