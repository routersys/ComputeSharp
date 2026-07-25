using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Helpers;

internal static class GeneratorHelper
{
    public static GeneratorDriver CreateDriver(IIncrementalGenerator generator, bool trackIncrementalGeneratorSteps = false)
    {
        return CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps));
    }

    public static ImmutableArray<GeneratedSourceResult> Run(GeneratorDriver driver, CSharpCompilation compilation, out GeneratorDriver resultDriver)
    {
        resultDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updatedCompilation, out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(System.Environment.NewLine, diagnostics));

        ImmutableArray<Diagnostic> compilationDiagnostics = updatedCompilation.GetDiagnostics();

        Assert.IsFalse(
            compilationDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            string.Join(
                System.Environment.NewLine,
                compilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        GeneratorRunResult result = resultDriver.GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return result.GeneratedSources;
    }

    public static string GetGeneratedSource(ImmutableArray<GeneratedSourceResult> sources, string hintNameFragment)
    {
        foreach (GeneratedSourceResult source in sources)
        {
            if (source.HintName.Contains(hintNameFragment))
            {
                return source.SourceText.ToString();
            }
        }

        Assert.Fail($"No generated source with hint name containing '{hintNameFragment}' was produced.");

        return "";
    }
}
