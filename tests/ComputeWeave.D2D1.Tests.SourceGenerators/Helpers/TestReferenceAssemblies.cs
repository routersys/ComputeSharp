using System.IO;
using Microsoft.CodeAnalysis.Testing;

namespace ComputeWeave.Tests.SourceGenerators.Helpers;

/// <summary>
/// The reference assemblies used for the compilations these tests create.
/// </summary>
/// <remarks>
/// <see cref="ReferenceAssemblies.Net"/> in the pinned testing packages stops at .NET 8. The assemblies
/// under test are built for <c>net10.0</c>, so a compilation over .NET 8 reference assemblies cannot
/// reference them, and every test fails with <c>CS1705</c> instead of exercising what it means to.
/// </remarks>
internal static class TestReferenceAssemblies
{
    /// <summary>
    /// The reference assemblies for <c>net10.0</c>.
    /// </summary>
    public static readonly ReferenceAssemblies Net100 = new(
        targetFramework: "net10.0",
        referenceAssemblyPackage: new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
        referenceAssemblyPath: Path.Combine("ref", "net10.0"));
}
