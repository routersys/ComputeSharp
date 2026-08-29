using System.Linq;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class LanguageVersionTests
{
    private const string ExtensionMemberSource = """
        internal static class Ext
        {
            extension(float value)
            {
                public float Doubled() => value * 2;
            }
        }
        """;

    private const string ExtensionIndexerSource = """
        internal static class Ext
        {
            extension(float[] values)
            {
                public float this[string key] => values[0];
            }
        }
        """;

    [TestMethod]
    public void CompilationsUseTheLanguageVersionConsumersGet()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation("internal class C;", "LanguageVersionDefaultTests");

        Assert.AreEqual(LanguageVersion.CSharp14, compilation.LanguageVersion);
    }

    [TestMethod]
    public void ARequestedLanguageVersionReachesTheCompilation()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            "internal class C;",
            "LanguageVersionRequestedTests",
            LanguageVersion.CSharp12);

        Assert.AreEqual(LanguageVersion.CSharp12, compilation.LanguageVersion);
    }

    [TestMethod]
    public void ExtensionMembersCanBeExpressed()
    {
        _ = CompilationHelper.CreateCompilation(ExtensionMemberSource, "LanguageVersionExtensionMemberTests");
    }

    [TestMethod]
    public void ExtensionIndexersAreAPreviewOnlyFeature()
    {
        CSharpCompilation shipping = CompilationHelper.CreateCompilationAllowingErrors(
            ExtensionIndexerSource,
            "LanguageVersionExtensionIndexerShippingTests");

        CSharpCompilation preview = CompilationHelper.CreateCompilationAllowingErrors(
            ExtensionIndexerSource,
            "LanguageVersionExtensionIndexerPreviewTests",
            LanguageVersion.Preview);

        Assert.IsTrue(
            shipping.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS8652"),
            string.Join(", ", shipping.GetDiagnostics().Select(static diagnostic => diagnostic.Id)));

        Assert.IsFalse(
            preview.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(", ", preview.GetDiagnostics().Select(static diagnostic => diagnostic.ToString())));
    }
}
