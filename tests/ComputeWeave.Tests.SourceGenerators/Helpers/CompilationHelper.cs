using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Helpers;

internal static class CompilationHelper
{
    public const LanguageVersion DefaultLanguageVersion = LanguageVersion.CSharp14;

    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    public static CSharpCompilation CreateCompilation(string source, string assemblyName, LanguageVersion languageVersion = DefaultLanguageVersion)
    {
        return CreateCompilation([source], assemblyName, languageVersion);
    }

    public static CSharpCompilation CreateCompilation(string[] sources, string assemblyName, LanguageVersion languageVersion = DefaultLanguageVersion)
    {
        return CreateCompilation(sources, assemblyName, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), languageVersion);
    }

    public static CSharpCompilation CreateCompilation(
        string[] sources,
        string assemblyName,
        CSharpCompilationOptions options,
        LanguageVersion languageVersion = DefaultLanguageVersion)
    {
        SyntaxTree[] syntaxTrees = new SyntaxTree[sources.Length];

        for (int i = 0; i < sources.Length; i++)
        {
            syntaxTrees[i] = ParseTree(sources[i], languageVersion);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            References,
            options);

        ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();

        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(Environment.NewLine, diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        return compilation;
    }

    /// <summary>
    /// Creates a compilation without requiring it to be free of errors, for the cases where what is
    /// being measured is how a generator behaves on source the C# compiler has already rejected.
    /// </summary>
    /// <param name="source">The source to compile.</param>
    /// <param name="assemblyName">The name to give the assembly.</param>
    /// <param name="languageVersion">The language version to compile <paramref name="source"/> with.</param>
    /// <returns>The compilation, errors and all.</returns>
    public static CSharpCompilation CreateCompilationAllowingErrors(
        string source,
        string assemblyName,
        LanguageVersion languageVersion = DefaultLanguageVersion)
    {
        return CSharpCompilation.Create(
            assemblyName,
            [ParseTree(source, languageVersion)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    public static SyntaxTree ParseTree(string source, LanguageVersion languageVersion = DefaultLanguageVersion)
    {
        return CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(languageVersion));
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        HashSet<string> locations = [];
        List<MetadataReference> references = [];

        foreach (string location in trustedAssemblies.Split(Path.PathSeparator))
        {
            if (location.Length == 0 ||
                !locations.Add(location) ||
                Path.GetFileName(location) is "ComputeWeave.SourceGenerators.dll")
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(location));
        }

        return [.. references];
    }
}
