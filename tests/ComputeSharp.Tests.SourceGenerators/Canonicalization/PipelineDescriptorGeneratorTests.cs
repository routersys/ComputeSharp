using System.Collections.Immutable;
using System.Linq;
using ComputeSharp.SourceGeneration.Constants;
using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineDescriptorGeneratorTests
{
    private const string HostSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device = null!;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

            [ComputePipeline]
            private void Run(in ComputeContext context)
            {
            }
        }
        """;

    private const string ResourceSetSource = """
        using System;
        using ComputeSharp;

        namespace Ukiyoe;

        public sealed class ExternalView : IDisposable
        {
            public void Dispose()
            {
            }
        }

        [ComputeInteropResourceSet]
        public sealed partial class ResourceSet
        {
            [ComputeSharedTexture(
                ComputeResourceResizePolicy.Exact,
                ComputeResourceAccess.ReadWrite,
                ExternalResourceAccess.Write,
                ExternalTextureUsage.RenderTarget,
                ComputeAlphaMode.Premultiplied,
                ComputeSharedTextureInitialOwner.External,
                ComputeResourceRecovery.RecreateFromHost)]
            private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> source;
        }
        """;

    private static string RunAndGetSource(string[] sources, string assemblyName, string hintNameFragment)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), hintNameFragment);
    }

    [TestMethod]
    public void EmitsCanonicalDescriptorForHost()
    {
        string source = RunAndGetSource([HostSource], "GeneratorHostTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("partial class Host"), source);
        Assert.IsTrue(source.Contains("private static global::System.ReadOnlySpan<byte> CanonicalDescriptor => ["), source);
        Assert.IsTrue(source.Contains("0x43, 0x53, 0x50, 0x31"), source);
    }

    [TestMethod]
    public void EmitsCanonicalDescriptorForResourceSet()
    {
        string source = RunAndGetSource([ResourceSetSource], "GeneratorResourceSetTests", "Ukiyoe.ResourceSet");

        Assert.IsTrue(source.Contains("partial class ResourceSet"), source);
        Assert.IsTrue(source.Contains("private static global::System.ReadOnlySpan<byte> CanonicalDescriptor => ["), source);
    }

    [TestMethod]
    public void EmitsNothingForInvalidHost()
    {
        const string InvalidHostSource = """
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device = null!;
            }
            """;

        CSharpCompilation compilation = CompilationHelper.CreateCompilation([InvalidHostSource], "GeneratorInvalidHostTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        Assert.AreEqual(0, GeneratorHelper.Run(driver, compilation, out _).Length);
    }

    [TestMethod]
    public void ProducesByteIdenticalSourceForCleanRuns()
    {
        string first = RunAndGetSource([HostSource], "GeneratorCleanFirstTests", "Ukiyoe.Host");
        string second = RunAndGetSource([HostSource], "GeneratorCleanSecondTests", "Ukiyoe.Host");

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void ProducesByteIdenticalSourceForIncrementalRun()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([HostSource], "GeneratorIncrementalTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator(), trackIncrementalGeneratorSteps: true);

        string clean = GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out GeneratorDriver cleanDriver), "Ukiyoe.Host");

        CSharpCompilation updatedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Ukiyoe;

            public sealed class Unrelated
            {
            }
            """));

        ImmutableArray<GeneratedSourceResult> incrementalSources = GeneratorHelper.Run(cleanDriver, updatedCompilation, out GeneratorDriver incrementalDriver);
        string incremental = GeneratorHelper.GetGeneratedSource(incrementalSources, "Ukiyoe.Host");

        Assert.AreEqual(clean, incremental);

        GeneratorRunResult result = incrementalDriver.GetRunResult().Results[0];

        Assert.AreNotEqual(
            0,
            result.TrackedOutputSteps.SelectMany(static step => step.Value).SelectMany(static step => step.Outputs).Count());

        Assert.IsTrue(
            result.TrackedOutputSteps
                .SelectMany(static step => step.Value)
                .SelectMany(static step => step.Outputs)
                .All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
            string.Join(
                ", ",
                result.TrackedOutputSteps
                    .SelectMany(static step => step.Value)
                    .SelectMany(static step => step.Outputs)
                    .Select(static output => output.Reason)));
    }

    [TestMethod]
    public void ReusesCachedOutputWhenUnrelatedSourceChanges()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([HostSource], "GeneratorCacheTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator(), trackIncrementalGeneratorSteps: true);

        _ = GeneratorHelper.Run(driver, compilation, out GeneratorDriver cleanDriver);

        CSharpCompilation updatedCompilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Ukiyoe;

            public sealed class Other
            {
            }
            """));

        _ = GeneratorHelper.Run(cleanDriver, updatedCompilation, out GeneratorDriver incrementalDriver);

        GeneratorRunResult result = incrementalDriver.GetRunResult().Results[0];

        Assert.IsTrue(result.TrackedSteps.ContainsKey(WellKnownTrackingNames.Execute));
        Assert.AreNotEqual(0, result.TrackedSteps[WellKnownTrackingNames.Execute].SelectMany(static step => step.Outputs).Count());
        Assert.IsTrue(
            result.TrackedSteps[WellKnownTrackingNames.Execute]
                .SelectMany(static step => step.Outputs)
                .All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
    }
}
