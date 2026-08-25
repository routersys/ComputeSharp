using System.Diagnostics;
using System.IO;
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
        string coreProjectPath = Path.Combine(RepositoryDirectory, "src", "ComputeWeave.Core", "ComputeWeave.Core.csproj");
        string projectPath = Path.Combine(RepositoryDirectory, "src", "ComputeWeave", "ComputeWeave.csproj");
        string dxcProjectPath = Path.Combine(RepositoryDirectory, "src", "ComputeWeave.Dxc", "ComputeWeave.Dxc.csproj");

        Process.Start("dotnet", $"pack {coreProjectPath} -c Release").WaitForExit();
        Process.Start("dotnet", $"pack {projectPath} -c Release").WaitForExit();
        Process.Start("dotnet", $"pack {dxcProjectPath} -c Release").WaitForExit();
    }
}