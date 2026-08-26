using ComputeWeave.Tests.NativeLibrariesResolver.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.NativeLibrariesResolver;

[TestClass]
public class NativeLibrariesResolverTests : NativeLibrariesResolverTestsBase
{
    /// <inheritdoc/>
    protected override string SampleProjectName => "ComputeWeave.NuGet";

    /// <summary>
    /// Performs static initialization for the assembly before any unit tests are run.
    /// </summary>
    /// <param name="_">The <see cref="TestContext"/> for the test runner instance in use.</param>
    [ClassInitialize]
    public static void InitializeDynamicDependencies(TestContext _)
    {
        PackProjects("ComputeWeave.Core", "ComputeWeave");
    }
}