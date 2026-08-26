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
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    public static CSharpCompilation CreateCompilation(string source, string assemblyName)
    {
        return CreateCompilation([source], assemblyName);
    }

    public static CSharpCompilation CreateCompilation(string[] sources, string assemblyName)
    {
        return CreateCompilation(sources, assemblyName, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static CSharpCompilation CreateCompilation(string[] sources, string assemblyName, CSharpCompilationOptions options)
    {
        SyntaxTree[] syntaxTrees = new SyntaxTree[sources.Length];

        for (int i = 0; i < sources.Length; i++)
        {
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(sources[i]);
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
    /// <returns>The compilation, errors and all.</returns>
    public static CSharpCompilation CreateCompilationAllowingErrors(string source, string assemblyName)
    {
        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
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
