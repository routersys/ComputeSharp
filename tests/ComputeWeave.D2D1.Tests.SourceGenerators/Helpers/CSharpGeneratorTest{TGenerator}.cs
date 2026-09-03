extern alias Core;

#if D2D1_TESTS || D2D1_WINUI_TESTS
#if D2D1_WINUI_TESTS
extern alias D2D1_WinUI;
#endif
extern alias D2D1;
#else
extern alias D3D12;
#endif

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Helpers;

/// <summary>
/// A helper type to run source generator tests.
/// </summary>
/// <typeparam name="TGenerator">The type of generator to test.</typeparam>
internal static class CSharpGeneratorTest<TGenerator>
    where TGenerator : IIncrementalGenerator, new()
{
    /// <summary>
    /// Verifies the resulting diagnostics from a source generator.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="diagnosticsIds">The expected diagnostics ids to be generated.</param>
    public static void VerifyDiagnostics(string source, params string[] diagnosticsIds)
    {
        RunGenerator(source, out Compilation compilation, out ImmutableArray<Diagnostic> diagnostics);

        Dictionary<string, Diagnostic> diagnosticMap = diagnostics.DistinctBy(diagnostic => diagnostic.Id).ToDictionary(diagnostic => diagnostic.Id);

        // Check that the diagnostics match
        Assert.IsTrue(diagnosticMap.Keys.ToHashSet().SetEquals(diagnosticsIds), $"Diagnostics didn't match. {string.Join(", ", diagnosticMap.Values)}");

        // If the compilation was supposed to succeed, ensure that no further errors were generated
        if (diagnosticsIds.Length == 0)
        {
            // Compute diagnostics for the final compiled output (just include errors)
            List<Diagnostic> outputCompilationDiagnostics = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();

            Assert.IsTrue(outputCompilationDiagnostics.Count == 0, $"resultingIds: {string.Join(", ", outputCompilationDiagnostics)}");
        }
    }

    /// <summary>
    /// Verifies that a source generator reports a given diagnostic, without requiring the whole set to match.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="diagnosticId">The diagnostic id expected to be among the reported ones.</param>
    /// <remarks>
    /// One input can trip several diagnostics, so the whole set is not asserted here.
    /// </remarks>
    public static void VerifyDiagnosticIsReported(string source, string diagnosticId)
    {
        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsTrue(
            diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            $"{diagnosticId} is not reported: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id).Distinct())}");
    }

    /// <summary>
    /// Verifies that a source generator does not report a given diagnostic.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="diagnosticId">The diagnostic id expected to be absent from the reported ones.</param>
    /// <remarks>
    /// The mirror of <see cref="VerifyDiagnosticIsReported"/>, for an input that trips other diagnostics as well,
    /// where asserting the whole set would fail whenever any of the others moves.
    /// </remarks>
    public static void VerifyDiagnosticIsNotReported(string source, string diagnosticId)
    {
        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsFalse(
            diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            $"{diagnosticId} is reported: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id).Distinct())}");
    }

    /// <summary>
    /// Runs a source generator over a source and gets the diagnostics it reports.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <returns>The diagnostics the generator reported for <paramref name="source"/>.</returns>
    public static ImmutableArray<Diagnostic> GetReportedDiagnostics(string source)
    {
        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics);

        return diagnostics;
    }

    /// <summary>
    /// Runs a source generator over a source parsed under a path, and gets the diagnostics it reports.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="path">The path to parse <paramref name="source"/> under.</param>
    /// <returns>The diagnostics the generator reported for <paramref name="source"/>.</returns>
    /// <remarks>
    /// A tree parsed without a path names no file, and a location captured by value names one, so only a
    /// caller measuring which tree a diagnostic lands in has anything to say about what the file is called.
    /// </remarks>
    public static ImmutableArray<Diagnostic> GetReportedDiagnostics(string source, string path)
    {
        RunGenerator(source, out _, out ImmutableArray<Diagnostic> diagnostics, path: path);

        return diagnostics;
    }

    /// <summary>
    /// Verifies the resulting sources produced by a source generator.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="result">The expected source to be generated.</param>
    /// <param name="languageVersion">The language version to use to run the test.</param>
    public static void VerifySources(string source, (string Filename, string Source) result, LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        RunGenerator(source, out Compilation compilation, out ImmutableArray<Diagnostic> diagnostics, languageVersion);

        // Ensure that no diagnostics were generated
        CollectionAssert.AreEquivalent(Array.Empty<Diagnostic>(), diagnostics);

        // Update the assembly version using the version from the assembly of the input generators.
        // This allows the tests to not need updates whenever the version of the MVVM Toolkit changes.
        string expectedText = result.Source.Replace("<ASSEMBLY_VERSION>", $"\"{typeof(TGenerator).Assembly.GetName().Version}\"");
        string actualText = compilation.SyntaxTrees.Single(tree => Path.GetFileName(tree.FilePath) == result.Filename).ToString();

        Assert.AreEqual(expectedText, actualText);
    }

    /// <summary>
    /// Verifies the incremental generator steps for a given source generator.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="updatedSource">The updated source to process.</param>
    /// <param name="executeReason">The reason for the first <c>"Execute"</c> step.</param>
    /// <param name="diagnosticsReason">The reason for the <c>"Diagnostics"</c> step.</param>
    /// <param name="outputReason">The reason for the <c>"Output"</c> step.</param>
    /// <param name="diagnosticsSourceReason">The reason for the output step for the diagnostics.</param>
    /// <param name="sourceReason">The reason for the final output source.</param>
    /// <param name="languageVersion">The language version to use to run the test.</param>
    public static void VerifyIncrementalSteps(
        string source,
        string updatedSource,
        IncrementalStepRunReason executeReason,
        IncrementalStepRunReason? diagnosticsReason,
        IncrementalStepRunReason outputReason,
        IncrementalStepRunReason? diagnosticsSourceReason,
        IncrementalStepRunReason sourceReason,
        LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        Compilation compilation = CreateCompilation(source, languageVersion);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new TGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true))
            .WithUpdatedParseOptions(compilation.SyntaxTrees.First().Options);

        // Run the generator on the initial sources
        driver = driver.RunGenerators(compilation);

        // Update the compilation by replacing the source
        compilation = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.First(),
            CSharpSyntaxTree.ParseText(updatedSource, CSharpParseOptions.Default.WithLanguageVersion(languageVersion)));

        // Run the generators again on the updated source
        driver = driver.RunGenerators(compilation);

        GeneratorRunResult result = driver.GetRunResult().Results.Single();

        // Get the generated sources and validate them. We have two possible cases: if no diagnostics
        // are produced, then just the output source node is triggered. Otherwise, we'll also have one
        // output node which is used to emit the gathered diagnostics from the initial transform step.
        if (diagnosticsSourceReason is not null)
        {
            Assert.AreEqual(
                expected: 2,
                actual:
                    result.TrackedOutputSteps
                    .SelectMany(outputStep => outputStep.Value)
                    .SelectMany(output => output.Outputs)
                    .Count());

            // The output step for the diagnostics is the one reached from the "Diagnostics" node, however
            // many nodes sit between them: the sequence is filtered, and then combined with the compilation.
            Assert.AreEqual(
                expected: diagnosticsSourceReason,
                actual:
                    result.TrackedOutputSteps
                    .Single().Value
                    .Single(run => ComesFrom(run, "Diagnostics"))
                    .Outputs.Single().Reason);

            Assert.AreEqual(
                expected: sourceReason,
                actual:
                    result.TrackedOutputSteps
                    .Single().Value
                    .Single(run => run.Inputs[0].Source.Name == "Output")
                    .Outputs.Single().Reason);
        }
        else
        {
            (object Value, IncrementalStepRunReason Reason)[] sourceOuputs =
                result.TrackedOutputSteps
                .SelectMany(outputStep => outputStep.Value)
                .SelectMany(output => output.Outputs)
                .ToArray();

            Assert.AreEqual(1, sourceOuputs.Length);
            Assert.AreEqual(sourceReason, sourceOuputs[0].Reason);
        }

        Assert.AreEqual(executeReason, result.TrackedSteps["Execute"].Single().Outputs[0].Reason);
        Assert.AreEqual(outputReason, result.TrackedSteps["Output"].Single().Outputs[0].Reason);

        // Check the diagnostics reason, which might not be present
        if (diagnosticsReason is not null)
        {
            Assert.AreEqual(diagnosticsReason, result.TrackedSteps["Diagnostics"].Single().Outputs[0].Reason);
        }
        else
        {
            Assert.IsFalse(result.TrackedSteps.ContainsKey("Diagnostics"));
        }
    }

    /// <summary>
    /// Checks whether an incremental step is reached from a node carrying a given tracking name.
    /// </summary>
    /// <param name="step">The step to walk up from.</param>
    /// <param name="name">The tracking name to look for.</param>
    /// <returns>Whether <paramref name="step"/> is reached from a node named <paramref name="name"/>.</returns>
    /// <remarks>
    /// Counting the nodes between two names instead would tie the assertion to the shape of the pipeline,
    /// which is not what it is measuring, and a node added between them reads as the step having vanished.
    /// </remarks>
    private static bool ComesFrom(IncrementalGeneratorRunStep step, string name)
    {
        if (step.Name == name)
        {
            return true;
        }

        foreach ((IncrementalGeneratorRunStep Source, int OutputIndex) input in step.Inputs)
        {
            if (ComesFrom(input.Source, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a compilation from a given source.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="languageVersion">The language version to use to run the test.</param>
    /// <param name="path">The path to parse <paramref name="source"/> under.</param>
    /// <returns>The resulting <see cref="Compilation"/> object.</returns>
    private static CSharpCompilation CreateCompilation(string source, LanguageVersion languageVersion = LanguageVersion.CSharp14, string path = "")
    {
        // Get all assembly references for the .NET TFM and ComputeWeave
        IEnumerable<MetadataReference> metadataReferences =
        [
            .. Net100.References.All,
            MetadataReference.CreateFromFile(typeof(Core::ComputeWeave.Hlsl).Assembly.Location),
#if D2D1_TESTS || D2D1_WINUI_TESTS
#if D2D1_WINUI_TESTS
            MetadataReference.CreateFromFile(typeof(D2D1_WinUI::ComputeWeave.D2D1.WinUI.CanvasEffect).Assembly.Location),
#endif
            MetadataReference.CreateFromFile(typeof(D2D1::ComputeWeave.D2D1.ID2D1PixelShader).Assembly.Location)
#else
            MetadataReference.CreateFromFile(typeof(D3D12::ComputeWeave.IComputeShader).Assembly.Location)
#endif
        ];

        // Parse the source text
        SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(languageVersion),
            path);

        // Create the original compilation
        return CSharpCompilation.Create(
            "original",
            [sourceTree],
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    /// <summary>
    /// Runs a generator and gathers the output results.
    /// </summary>
    /// <param name="source">The input source to process.</param>
    /// <param name="compilation"><inheritdoc cref="GeneratorDriver.RunGeneratorsAndUpdateCompilation" path="/param[@name='outputCompilation']/node()"/></param>
    /// <param name="diagnostics"><inheritdoc cref="GeneratorDriver.RunGeneratorsAndUpdateCompilation" path="/param[@name='diagnostics']/node()"/></param>
    /// <param name="languageVersion">The language version to use to run the test.</param>
    /// <param name="path">The path to parse <paramref name="source"/> under.</param>
    private static void RunGenerator(
        string source,
        out Compilation compilation,
        out ImmutableArray<Diagnostic> diagnostics,
        LanguageVersion languageVersion = LanguageVersion.CSharp14,
        string path = "")
    {
        Compilation originalCompilation = CreateCompilation(source, languageVersion, path);

        // Create the generator driver with the D2D shader generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator()).WithUpdatedParseOptions(originalCompilation.SyntaxTrees.First().Options);

        // Run all source generators on the input source code
        _ = driver.RunGeneratorsAndUpdateCompilation(originalCompilation, out compilation, out diagnostics);
    }
}