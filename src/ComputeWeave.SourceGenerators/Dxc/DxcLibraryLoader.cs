using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

#pragma warning disable RS1035

namespace ComputeWeave.SourceGenerators.Dxc;

/// <summary>
/// A <see langword="class"/> that handles loading the DXC libraries.
/// </summary>
internal sealed unsafe class DxcLibraryLoader
{
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
        static string ExtractLibrary(string folder, string rid, string name)
        {
            string sourceFilename = $"ComputeWeave.SourceGenerators.ComputeWeave.Libraries.{rid}.{name}.dll";
            string targetFilename = Path.Combine(folder, rid, $"{name}.dll");

            using Stream sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourceFilename);

            ExtractLibraryAtomically(sourceStream, targetFilename);

            return targetFilename;
        }

        static string GetCacheKey(string rid)
        {
            using Stream dxilStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ComputeWeave.SourceGenerators.ComputeWeave.Libraries.{rid}.dxil.dll");
            using Stream dxcompilerStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ComputeWeave.SourceGenerators.ComputeWeave.Libraries.{rid}.dxcompiler.dll");

            return DxcLibraryLoader.GetCacheKey(dxilStream, dxcompilerStream);
        }

        // Loads a target native library
        static unsafe void LoadLibrary(string filename)
        {
            [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
            static extern void* LoadLibraryW(ushort* lpLibFileName);

            fixed (char* p = filename)
            {
                if (LoadLibraryW((ushort*)p) is null)
                {
                    int hresult = Marshal.GetLastWin32Error();

                    throw new Win32Exception(hresult, $"Failed to load {Path.GetFileName(filename)}.");
                }
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

            string folder = Path.Combine(Path.GetTempPath(), "ComputeWeave.SourceGenerators", "Dxc", GetCacheKey(rid));

            LoadLibrary(ExtractLibrary(folder, rid, "dxil"));
            LoadLibrary(ExtractLibrary(folder, rid, "dxcompiler"));

            areDxcLibrariesLoaded = true;
        }
    }

    internal static void ExtractLibraryAtomically(Stream sourceStream, string targetFilename)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(targetFilename));

        if (File.Exists(targetFilename))
        {
            return;
        }

        string temporaryFilename = Path.Combine(Path.GetDirectoryName(targetFilename), Path.GetRandomFileName());
        bool isTemporaryFileCreated = false;

        try
        {
            using (Stream destinationStream = File.Open(temporaryFilename, FileMode.CreateNew, FileAccess.Write))
            {
                isTemporaryFileCreated = true;

                sourceStream.CopyTo(destinationStream);
            }

            try
            {
                File.Move(temporaryFilename, targetFilename);
            }
            catch (IOException) when (File.Exists(targetFilename))
            {
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

    internal static string GetCacheKey(Stream dxilStream, Stream dxcompilerStream)
    {
        byte[] resourceHashes = new byte[64];

        using SHA256 hashAlgorithm = SHA256.Create();

        Buffer.BlockCopy(hashAlgorithm.ComputeHash(dxilStream), 0, resourceHashes, 0, 32);
        Buffer.BlockCopy(hashAlgorithm.ComputeHash(dxcompilerStream), 0, resourceHashes, 32, 32);

        return BitConverter.ToString(hashAlgorithm.ComputeHash(resourceHashes)).Replace("-", string.Empty);
    }
}
