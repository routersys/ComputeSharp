using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ComputeWeave.Win32;

namespace ComputeWeave.Interop;

/// <inheritdoc/>
partial class ReflectionServices
{
    /// <summary>
    /// Initializes the DLL resolvers for the dxcompiler.dll and dxil.dll libraries.
    /// </summary>
    static ReflectionServices()
    {
        // Register a custom library resolver for the two DXC libraries. We need to either manually load the two
        // libraries from the NuGet directory, if an RID is not in use, or we need to ensure that dxil.dll is
        // loaded correctly in case the program was executed with the host being in another directory.
        // This happens when doing eg. "dotnet bin\Debug\net8.0\MyApp.dll", which would crash at runtime.
        //
        // The resolver has to go on the assembly that declares the "dxcompiler" import, which is the one holding
        // the Win32 bindings and not this one. A resolver only ever runs for P/Invokes declared by the assembly it
        // is registered on, so registering it here resolved nothing. What this actually buys is the pre-load of
        // dxil.dll: "dxcompiler" is found by the default probing either way, but "dxil" is never imported from
        // managed code at all. It is loaded by dxcompiler.dll itself through LoadLibrary, which does not know
        // about the app directory, so without this the copy sitting next to the app is not the one that is used.
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(DirectX).Assembly, OnDllImport);
        }
        catch (InvalidOperationException)
        {
            // An assembly can only ever carry one resolver, and consumers may register their own on it. Losing
            // that race leaves the default probing in charge, which is exactly the behavior without this type.
            // Letting it propagate would be worse: a failed type initializer is cached, so every later call into
            // this type would throw TypeInitializationException instead of merely skipping the pre-load.
        }
    }

    /// <summary>
    /// The custom <see cref="DllImportResolver"/> for the assembly declaring the DXC imports.
    /// </summary>
    /// <inheritdoc cref="DllImportResolver"/>
    private static IntPtr OnDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // This resolver is registered on the assembly holding the Win32 bindings, so it is consulted for every
        // P/Invoke that assembly declares, not just this one. Returning zero for the others is what keeps them
        // on the default probing path, so this early return has to stay.
        if (libraryName is not "dxcompiler")
        {
            return IntPtr.Zero;
        }

        string rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => default(NotSupportedException).Throw<string>()
        };

        // Test whether the native libraries are present in the same folder of the executable
        // (which is the case when the program was built with a runtime identifier), or whether
        // they are in the "runtimes\win-x64\native" folder in the executable directory.
        string nugetNativeLibsPath = Path.Combine(AppContext.BaseDirectory, $@"runtimes\{rid}\native");
        bool isNuGetRuntimeLibrariesDirectoryPresent = Directory.Exists(nugetNativeLibsPath);

        if (isNuGetRuntimeLibrariesDirectoryPresent)
        {
            string dxcompilerPath = Path.Combine(AppContext.BaseDirectory, $@"runtimes\{rid}\native\dxcompiler.dll");
            string dxilPath = Path.Combine(AppContext.BaseDirectory, $@"runtimes\{rid}\native\dxil.dll");

            // Load DXIL first so that DXC doesn't fail to load it, and then DXIL, both from the NuGet path
            if (NativeLibrary.TryLoad(dxilPath, out _) &&
                NativeLibrary.TryLoad(dxcompilerPath, out IntPtr handle))
            {
                return handle;
            }
        }
        else
        {
            // Even when the two libraries are correctly copied next to the executable in use, we load them
            // manually to ensure the operation is successful. This is to avoid failures in cases such as when
            // doing "dotnet bin\MyApp.dll", ie. when the host is in another path than the executable in use.
            // This is probably because DXIL is a native dependency for DXC, but the way Windows loads these
            // libraries doesn't take into account the .NET concepts of "app directory": neither the current "bin"
            // directory nor the "process directory", which is "C:\Program Files\dotnet", actually contain the
            // native library we need, hence the runtime crash. Manually loading the library this way solves this.
            if (NativeLibrary.TryLoad("dxil", assembly, searchPath, out _) &&
                NativeLibrary.TryLoad("dxcompiler", assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }
}