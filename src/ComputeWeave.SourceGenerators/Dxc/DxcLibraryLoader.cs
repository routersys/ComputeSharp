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
    /// <summary>
    /// The number of times publishing an extracted library into the cache is retried.
    /// </summary>
    private const int CachePublicationRetryCount = 500;

    /// <summary>
    /// The delay between two attempts at publishing an extracted library into the cache.
    /// </summary>
    private const int CachePublicationRetryDelayMilliseconds = 10;

    /// <summary>
    /// The number of times opening a cached library is retried.
    /// </summary>
    private const int CacheOpenRetryCount = 100;

    /// <summary>
    /// The delay between two attempts at opening a cached library.
    /// </summary>
    private const int CacheOpenRetryDelayMilliseconds = 1;

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

    /// <summary>
    /// Extracts a native library into the shared cache, so that only a verified copy is ever observed.
    /// </summary>
    /// <param name="sourceStream">The embedded library to extract.</param>
    /// <param name="expectedHash">The hash <paramref name="sourceStream"/> is expected to have.</param>
    /// <param name="targetFilename">The cached path the library is published at.</param>
    /// <returns>A read handle over the cached library, holding it in place until it is disposed.</returns>
    /// <exception cref="InvalidDataException">Thrown if the extracted or published library does not match <paramref name="expectedHash"/>.</exception>
    /// <exception cref="IOException">Thrown if the library could not be published into the cache.</exception>
    /// <remarks>
    /// The library is written to a temporary file of the cache folder, verified there, then moved over the
    /// cached path, so a concurrent process observes either the previous copy or the whole new one. A copy
    /// another process published first is reused as is, and a corrupt one is replaced.
    /// </remarks>
    internal static FileStream ExtractLibraryAtomically(Stream sourceStream, byte[] expectedHash, string targetFilename)
    {
        string folder = Path.GetDirectoryName(targetFilename);

        _ = Directory.CreateDirectory(folder);

        if (TryOpenVerifiedLibrary(targetFilename, expectedHash, out _) is FileStream existingLibrary)
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

            for (int attempt = 0; ; attempt++)
            {
                if (TryOpenVerifiedLibrary(targetFilename, expectedHash, out _) is FileStream publishedLibrary)
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

            for (int attempt = 0; ; attempt++)
            {
                if (TryOpenVerifiedLibrary(targetFilename, expectedHash, out bool isAbsent) is FileStream verifiedLibrary)
                {
                    return verifiedLibrary;
                }

                if (!isAbsent || attempt == CacheOpenRetryCount)
                {
                    throw new InvalidDataException("The published DXC library does not match its embedded source.");
                }

                Thread.Sleep(CacheOpenRetryDelayMilliseconds);
            }
        }
        finally
        {
            if (isTemporaryFileCreated)
            {
                File.Delete(temporaryFilename);
            }
        }
    }

    /// <summary>
    /// Gets the folder the libraries of a given cache key are extracted into.
    /// </summary>
    /// <param name="localApplicationData">The local application data directory of the current user.</param>
    /// <param name="cacheKey">The cache key of the embedded libraries.</param>
    /// <returns>The folder the libraries of <paramref name="cacheKey"/> are extracted into.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="localApplicationData"/> is not a rooted path.</exception>
    internal static string GetCacheFolder(string localApplicationData, string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData) || !Path.IsPathRooted(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is not available.");
        }

        return Path.Combine(localApplicationData, "ComputeWeave", "SourceGenerators", "Dxc", cacheKey);
    }

    /// <summary>
    /// Gets the cache key of a pair of embedded libraries.
    /// </summary>
    /// <param name="dxilStream">The embedded <c>dxil.dll</c> library.</param>
    /// <param name="dxcompilerStream">The embedded <c>dxcompiler.dll</c> library.</param>
    /// <returns>The cache key of the two embedded libraries.</returns>
    internal static string GetCacheKey(Stream dxilStream, Stream dxcompilerStream)
    {
        return GetCacheKey(GetHash(dxilStream), GetHash(dxcompilerStream));
    }

    /// <summary>
    /// Gets the cache key of a pair of embedded library hashes.
    /// </summary>
    /// <param name="dxilHash">The hash of the embedded <c>dxil.dll</c> library.</param>
    /// <param name="dxcompilerHash">The hash of the embedded <c>dxcompiler.dll</c> library.</param>
    /// <returns>The cache key of the two embedded libraries.</returns>
    /// <remarks>
    /// The two hashes have a fixed length, so concatenating them keeps the boundary between the libraries and
    /// no pair of different libraries produces the key of another one.
    /// </remarks>
    private static string GetCacheKey(byte[] dxilHash, byte[] dxcompilerHash)
    {
        byte[] resourceHashes = new byte[dxilHash.Length + dxcompilerHash.Length];

        Buffer.BlockCopy(dxilHash, 0, resourceHashes, 0, dxilHash.Length);
        Buffer.BlockCopy(dxcompilerHash, 0, resourceHashes, dxilHash.Length, dxcompilerHash.Length);

        using SHA256 hashAlgorithm = SHA256.Create();

        return BitConverter.ToString(hashAlgorithm.ComputeHash(resourceHashes)).Replace("-", string.Empty);
    }

    /// <summary>
    /// Gets the hash of the remaining content of a stream.
    /// </summary>
    /// <param name="stream">The stream to hash the remaining content of.</param>
    /// <returns>The hash of the remaining content of <paramref name="stream"/>.</returns>
    private static byte[] GetHash(Stream stream)
    {
        using SHA256 hashAlgorithm = SHA256.Create();

        return hashAlgorithm.ComputeHash(stream);
    }

    /// <summary>
    /// Opens a cached library, if it is present and matches its embedded source.
    /// </summary>
    /// <param name="targetFilename">The cached path of the library.</param>
    /// <param name="expectedHash">The hash the cached library is expected to have.</param>
    /// <param name="isAbsent">Whether the cached library was absent rather than corrupt.</param>
    /// <returns>A read handle over the cached library, or <see langword="null"/> if it is absent or corrupt.</returns>
    /// <remarks>
    /// Another process publishing the library holds it while it does so, so the open is retried before the
    /// library is reported as absent.
    /// </remarks>
    private static FileStream? TryOpenVerifiedLibrary(string targetFilename, byte[] expectedHash, out bool isAbsent)
    {
        isAbsent = false;

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

                isAbsent = true;

                return null;
            }
            catch (IOException) when (attempt < CacheOpenRetryCount)
            {
                stream?.Dispose();

                Thread.Sleep(CacheOpenRetryDelayMilliseconds);
            }
            catch
            {
                stream?.Dispose();

                throw;
            }
        }
    }

    /// <summary>
    /// Checks whether two hashes match.
    /// </summary>
    /// <param name="left">The first hash to compare.</param>
    /// <param name="right">The second hash to compare.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> match.</returns>
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
