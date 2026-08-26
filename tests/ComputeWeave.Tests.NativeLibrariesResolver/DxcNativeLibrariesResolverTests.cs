using System;
using System.IO;
using System.IO.Compression;
using ComputeWeave.Tests.NativeLibrariesResolver.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.NativeLibrariesResolver;

[TestClass]
public class DxcNativeLibrariesResolverTests : NativeLibrariesResolverTestsBase
{
    /// <inheritdoc/>
    protected override string SampleProjectName => "ComputeWeave.Dxc.NuGet";

    /// <summary>
    /// Performs static initialization for the assembly before any unit tests are run.
    /// </summary>
    /// <param name="_">The <see cref="TestContext"/> for the test runner instance in use.</param>
    [ClassInitialize]
    public static void InitializeDynamicDependencies(TestContext _)
    {
        PackProjects("ComputeWeave.Core", "ComputeWeave", "ComputeWeave.Dxc");
    }

    /// <summary>
    /// Checks that the packed 'ComputeWeave.Dxc' package carries the native libraries for the runtime the
    /// sample project is exercised on.
    /// </summary>
    /// <remarks>
    /// The other tests in this class only observe the exit code of the sample project, and that alone cannot
    /// tell whether the package delivered the libraries. When the package carries none, resolution falls to
    /// the default probing order and binds whichever copy of DXC the machine has, such as the one installed
    /// with the Windows SDK. The sample project keeps working, so a package that stopped carrying the
    /// libraries would go unnoticed on every machine that has another copy. Reading the package itself
    /// removes that dependency on the machine, and covers every cause rather than one of them.
    /// Only the runtime the sample project is published for is checked here, because that is the only one
    /// these tests deploy and load.
    /// </remarks>
    [TestMethod]
    public void PackagedNativeLibrariesAreCarriedForTheTargetRuntime()
    {
        string packagePath = Path.Combine(PackageDirectory, $"ComputeWeave.Dxc.{PackageVersion}.nupkg");

        Assert.IsTrue(File.Exists(packagePath), $"The package was not packed to '{packagePath}'.");

        using ZipArchive package = ZipFile.OpenRead(packagePath);

        foreach (string entryName in (ReadOnlySpan<string>)["runtimes/win-x64/native/dxcompiler.dll", "runtimes/win-x64/native/dxil.dll"])
        {
            Assert.IsNotNull(package.GetEntry(entryName), $"'{packagePath}' does not carry '{entryName}'.");
        }
    }
}