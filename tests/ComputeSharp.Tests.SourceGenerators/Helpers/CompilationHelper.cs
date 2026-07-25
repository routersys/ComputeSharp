using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Helpers;

internal static class CompilationHelper
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    public static CSharpCompilation CreateCompilation(string source, string assemblyName)
    {
        return CreateCompilation([source], assemblyName);
    }

    public static CSharpCompilation CreateCompilation(string[] sources, string assemblyName)
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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();

        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(Environment.NewLine, diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        return compilation;
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
                Path.GetFileName(location) is "ComputeSharp.SourceGenerators.dll")
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(location));
        }

        return [.. references];
    }
}
