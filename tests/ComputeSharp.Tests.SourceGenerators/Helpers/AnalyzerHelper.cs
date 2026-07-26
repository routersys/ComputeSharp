using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Helpers;

internal static class AnalyzerHelper
{
    public static void AssertDiagnostics(DiagnosticAnalyzer analyzer, string[] sources, string assemblyName, params string[] expectedIds)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);

        ImmutableArray<Diagnostic> diagnostics = compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        string[] actualIds = [.. diagnostics.Select(static diagnostic => diagnostic.Id).Order()];

        Array.Sort(expectedIds, StringComparer.Ordinal);

        CollectionAssert.AreEqual(
            expectedIds,
            actualIds,
            string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString())));
    }
}
