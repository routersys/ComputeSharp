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
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
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
            if (location.Length == 0 || !locations.Add(location))
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(location));
        }

        return [.. references];
    }
}
