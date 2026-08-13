using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

#pragma warning disable RS1035

namespace ComputeWeave.SourceGenerators.Dxc;

/// <summary>
/// A <see langword="class"/> that handles loading the DXC libraries.
/// </summary>
internal sealed unsafe class DxcLibraryLoader
{
    private const int CachePublicationRetryCount = 500;

    private const int CachePublicationRetryDelayMilliseconds = 10;

    /// <summary>
    /// An object to use to synchronize loading the DXC libraries.
    /// </summary>
    private static readonly object LoadingLock = new();

    /// <summary>
    /// Indicates whether the required <c>dxcompiler.dll</c> and <c>dxil.dll</c> libraries have been loaded.
    /// </summary>
    private static volatile bool areDxcLibrariesLoaded;

    /// <summary>
    /// Extracts and loads the <c>dxcompiler.dll</c> and <c>dxil.dll</c> libraries.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if the CPU architecture is not supported.</exception>
    /// <exception cref="Win32Exception">Thrown if a library fails to load.</exception>
    public static void LoadNativeDxcLibraries()
    {
        // Extracts a specified native library for a given runtime identifier
        static FileStream ExtractLibrary(string folder, string rid, string name, byte[] expectedHash)
        {
            string sourceFilename = $"ComputeWeave.SourceGenerators.ComputeWeave.Libraries.{rid}.{name}.dll";
            string targetFilename = Path.Combine(folder, rid, $"{name}.dll");

            using Stream sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourceFilename);

            return ExtractLibraryAtomically(sourceStream, expectedHash, targetFilename);
        }

        static byte[] GetResourceHash(string rid, string name)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ComputeWeave.SourceGenerators.ComputeWeave.Libraries.{rid}.{name}.dll");

            return GetHash(stream);
        }

        // Loads a target native library
        static unsafe void* LoadLibrary(string filename)
        {
            [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
            static extern void* LoadLibraryExW(ushort* lpLibFileName, void* hFile, uint dwFlags);

            const uint LoadLibrarySearchDllLoadDir = 0x00000100;
            const uint LoadLibrarySearchSystem32 = 0x00000800;

            filename = Path.GetFullPath(filename);

            fixed (char* p = filename)
            {
                void* module = LoadLibraryExW(
                    (ushort*)p,
                    null,
                    LoadLibrarySearchDllLoadDir | LoadLibrarySearchSystem32);

                if (module is null)
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    throw new Win32Exception(errorCode, $"Failed to load {Path.GetFileName(filename)}.");
                }

                return module;
            }
        }

        if (areDxcLibrariesLoaded)
        {
            return;
        }

        lock (LoadingLock)
        {
            if (areDxcLibrariesLoaded)
            {
                return;
            }

            string rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => throw new NotSupportedException("Invalid process architecture")
            };

            byte[] dxilHash = GetResourceHash(rid, "dxil");
            byte[] dxcompilerHash = GetResourceHash(rid, "dxcompiler");
            string folder = GetCacheFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                GetCacheKey(dxilHash, dxcompilerHash));

            using FileStream dxilLibrary = ExtractLibrary(folder, rid, "dxil", dxilHash);
            using FileStream dxcompilerLibrary = ExtractLibrary(folder, rid, "dxcompiler", dxcompilerHash);

            void* dxilModule = LoadLibrary(dxilLibrary.Name);

            try
            {
                _ = LoadLibrary(dxcompilerLibrary.Name);
            }
            catch
            {
                [DllImport("kernel32", ExactSpelling = true)]
                static extern int FreeLibrary(void* hLibModule);

                _ = FreeLibrary(dxilModule);

                throw;
            }

            areDxcLibrariesLoaded = true;
        }
    }

    internal static FileStream ExtractLibraryAtomically(Stream sourceStream, byte[] expectedHash, string targetFilename)
    {
        string folder = Path.GetDirectoryName(targetFilename);

        _ = Directory.CreateDirectory(folder);

        if (TryOpenVerifiedLibrary(targetFilename, expectedHash) is FileStream existingLibrary)
        {
            return existingLibrary;
        }

        string temporaryFilename = Path.Combine(folder, Path.GetRandomFileName());
        bool isTemporaryFileCreated = false;

        try
        {
            using (FileStream destinationStream = new(temporaryFilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                isTemporaryFileCreated = true;

                sourceStream.CopyTo(destinationStream);
                destinationStream.Flush(flushToDisk: true);
                destinationStream.Position = 0;

                if (!HashesMatch(GetHash(destinationStream), expectedHash))
                {
                    throw new InvalidDataException("The extracted DXC library does not match its embedded source.");
                }
            }

            if (TryOpenVerifiedLibrary(targetFilename, expectedHash) is FileStream competingLibrary)
            {
                return competingLibrary;
            }

            for (int attempt = 0; ; attempt++)
            {
                if (TryOpenVerifiedLibrary(targetFilename, expectedHash) is FileStream publishedLibrary)
                {
                    return publishedLibrary;
                }

                try
                {
                    if (File.Exists(targetFilename))
                    {
                        File.Replace(temporaryFilename, targetFilename, null);
                    }
                    else
                    {
                        File.Move(temporaryFilename, targetFilename);
                    }

                    break;
                }
                catch (IOException) when (attempt < CachePublicationRetryCount)
                {
                    Thread.Sleep(CachePublicationRetryDelayMilliseconds);
                }
            }

            return TryOpenVerifiedLibrary(targetFilename, expectedHash) ??
                throw new InvalidDataException("The published DXC library does not match its embedded source.");
        }
        finally
        {
            if (isTemporaryFileCreated)
            {
                File.Delete(temporaryFilename);
            }
        }
    }

    internal static string GetCacheFolder(string localApplicationData, string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData) || !Path.IsPathRooted(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is not available.");
        }

        return Path.Combine(localApplicationData, "ComputeWeave", "SourceGenerators", "Dxc", cacheKey);
    }

    internal static string GetCacheKey(Stream dxilStream, Stream dxcompilerStream)
    {
        return GetCacheKey(GetHash(dxilStream), GetHash(dxcompilerStream));
    }

    private static string GetCacheKey(byte[] dxilHash, byte[] dxcompilerHash)
    {
        byte[] resourceHashes = new byte[dxilHash.Length + dxcompilerHash.Length];

        Buffer.BlockCopy(dxilHash, 0, resourceHashes, 0, dxilHash.Length);
        Buffer.BlockCopy(dxcompilerHash, 0, resourceHashes, dxilHash.Length, dxcompilerHash.Length);

        using SHA256 hashAlgorithm = SHA256.Create();

        return BitConverter.ToString(hashAlgorithm.ComputeHash(resourceHashes)).Replace("-", string.Empty);
    }

    private static byte[] GetHash(Stream stream)
    {
        using SHA256 hashAlgorithm = SHA256.Create();

        return hashAlgorithm.ComputeHash(stream);
    }

    private static FileStream? TryOpenVerifiedLibrary(string targetFilename, byte[] expectedHash)
    {
        for (int attempt = 0; ; attempt++)
        {
            FileStream? stream = null;

            try
            {
                stream = new FileStream(targetFilename, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (!HashesMatch(GetHash(stream), expectedHash))
                {
                    stream.Dispose();

                    return null;
                }

                stream.Position = 0;

                return stream;
            }
            catch (FileNotFoundException)
            {
                stream?.Dispose();

                return null;
            }
            catch (IOException) when (attempt < 100)
            {
                stream?.Dispose();

                Thread.Sleep(1);
            }
            catch
            {
                stream?.Dispose();

                throw;
            }
        }
    }

    private static bool HashesMatch(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        int difference = 0;

        for (int i = 0; i < left.Length; i++)
        {
            difference |= left[i] ^ right[i];
        }

        return difference == 0;
    }
}
