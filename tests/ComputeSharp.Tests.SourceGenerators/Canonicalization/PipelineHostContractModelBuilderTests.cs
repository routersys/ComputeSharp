using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineHostContractModelBuilderTests
{
    private const string SlotSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        public sealed partial class Host
        {
            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

            [ComputePipelineResource(ComputeResourceAccess.Read)]
            private readonly ReadOnlyBuffer<float> weights = null!;
        }
        """;

    private static PipelineHostContractInfo Build(string[] sources, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol hostSymbol = compilation.GetTypeByMetadataName("Ukiyoe.Host")!;

        Assert.IsNotNull(hostSymbol);
        Assert.IsTrue(PipelineHostContractModelBuilder.TryBuild(hostSymbol, symbols, out PipelineHostContractInfo host));

        return host;
    }

    private static bool TryBuild(string[] sources, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol hostSymbol = compilation.GetTypeByMetadataName("Ukiyoe.Host")!;

        Assert.IsNotNull(hostSymbol);

        return PipelineHostContractModelBuilder.TryBuild(hostSymbol, symbols, out _);
    }

    private static string HostSource(string pipelines, int maximumConcurrentInvocations = 1)
    {
        return $$"""
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputePipelineHost("device", {{maximumConcurrentInvocations}})]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device = null!;

            {{pipelines}}
            }
            """;
    }

    [TestMethod]
    public void BuildsHostWithReplicatedInternalResources()
    {
        PipelineHostContractInfo host = Build(
            [
                SlotSource,
                HostSource("""
                        [ComputePipeline]
                        private void Second(in ComputeContext context, [ComputeResource(ComputeResourceAccess.Read)] ReadOnlyBuffer<int> source)
                        {
                        }

                        [ComputePipeline]
                        private void First(in ComputeContext context)
                        {
                        }
                    """)
            ],
            "HostContractModelReplicatedTests");

        Assert.AreEqual("Ukiyoe.Host", host.HostTypeMetadataName);
        Assert.AreEqual(1, host.MaximumConcurrentInvocations);
        Assert.AreEqual(1, host.Slots.Length);

        ImmutableArray<PipelineContractInfo> pipelines = host.Pipelines.AsImmutableArray();

        Assert.AreEqual(2, pipelines.Length);
        Assert.AreEqual("First", pipelines[0].MethodMetadataName);
        Assert.AreEqual(0u, pipelines[0].Ordinal);
        Assert.AreEqual("Second", pipelines[1].MethodMetadataName);
        Assert.AreEqual(1u, pipelines[1].Ordinal);

        Assert.AreEqual(0, pipelines[0].Parameters.Length);
        Assert.AreEqual(2, pipelines[0].InternalResources.Length);
        Assert.AreEqual(1, pipelines[1].Parameters.Length);
        Assert.AreEqual(2, pipelines[1].InternalResources.Length);

        Assert.AreEqual(pipelines[0].InternalResources.AsImmutableArray()[0].ResourceTypeMetadataName, pipelines[1].InternalResources.AsImmutableArray()[0].ResourceTypeMetadataName);

        Assert.AreEqual(2, pipelines[0].MaximumTrackedResourceCount);
        Assert.AreEqual(2, pipelines[0].MaximumCommandListSegments);
        Assert.AreEqual(3, pipelines[1].MaximumTrackedResourceCount);
        Assert.AreEqual(2, pipelines[1].MaximumCommandListSegments);

        Assert.AreEqual(3, host.Structural.MaximumTrackedResourceCount);
        Assert.AreEqual(2, host.Structural.MaximumCommandListSegments);
        Assert.AreEqual(1, host.Structural.OwnedSlotCount);
    }

    [TestMethod]
    public void ResolvesSlotOrdinalsAndOrdinalsAcrossParametersAndInternals()
    {
        PipelineHostContractInfo host = Build(
            [
                SlotSource,
                HostSource("""
                        [ComputePipeline]
                        private void Run(in ComputeContext context, [ComputeResource(ComputeResourceAccess.Read)] ReadOnlyBuffer<int> source)
                        {
                        }
                    """)
            ],
            "HostContractModelSlotOrdinalTests");

        ImmutableArray<ResourceContractInfo> parameters = host.Pipelines.AsImmutableArray()[0].Parameters.AsImmutableArray();
        ImmutableArray<ResourceContractInfo> internalResources = host.Pipelines.AsImmutableArray()[0].InternalResources.AsImmutableArray();

        Assert.AreEqual(0u, parameters[0].Ordinal);
        Assert.AreEqual(ResourceOwnershipKind.Borrowed, parameters[0].Ownership);
        Assert.IsFalse(parameters[0].HasSlot);

        Assert.AreEqual(1u, internalResources[0].Ordinal);
        Assert.AreEqual("index", host.Slots.AsImmutableArray()[0].MemberMetadataName);
        Assert.AreEqual(ResourceOwnershipKind.OwnedSlot, internalResources[0].Ownership);
        Assert.IsTrue(internalResources[0].HasSlot);
        Assert.AreEqual(0u, internalResources[0].Slot);
        Assert.AreEqual(0u, internalResources[0].SlotResourceIndex);

        Assert.AreEqual(2u, internalResources[1].Ordinal);
        Assert.AreEqual(ResourceOwnershipKind.Borrowed, internalResources[1].Ownership);
        Assert.IsFalse(internalResources[1].HasSlot);
        Assert.AreEqual(0u, internalResources[1].Slot);
    }

    [TestMethod]
    public void KeepsExternalParameterBorrowedWithoutSlot()
    {
        PipelineHostContractInfo host = Build(
            [
                SlotSource,
                HostSource("""
                        [ComputeInterop]
                        [ComputePipeline]
                        private void Run(
                            in ComputeContext context,
                            [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> source)
                        {
                        }
                    """)
            ],
            "HostContractModelExternalTests");

        PipelineContractInfo pipeline = host.Pipelines.AsImmutableArray()[0];
        ResourceContractInfo parameter = pipeline.Parameters.AsImmutableArray()[0];

        Assert.AreEqual(ComputeResourceSharing.External, parameter.Sharing);
        Assert.AreEqual(ResourceOwnershipKind.Borrowed, parameter.Ownership);
        Assert.IsFalse(parameter.HasSlot);
        Assert.AreEqual(0u, parameter.Slot);
        Assert.AreEqual(0u, parameter.SlotResourceIndex);

        Assert.AreEqual(PipelineFlags.InteropRoundTrip, pipeline.Flags);
        Assert.AreEqual(3, pipeline.MaximumCommandListSegments);
    }

    [TestMethod]
    public void ProducesSameModelForReversedSyntaxTreeOrder()
    {
        string pipelines = """
                    [ComputePipeline]
                    private void Run(in ComputeContext context)
                    {
                    }
            """;

        PipelineHostContractInfo forward = Build([SlotSource, HostSource(pipelines)], "HostContractModelForwardTests");
        PipelineHostContractInfo reversed = Build([HostSource(pipelines), SlotSource], "HostContractModelReversedTests");

        Assert.AreEqual(forward, reversed);
    }

    [TestMethod]
    public void RejectsHostWithoutPipelines()
    {
        Assert.IsFalse(TryBuild([SlotSource, HostSource("")], "HostContractModelNoPipelineTests"));
    }

    [TestMethod]
    public void RejectsPipelineWithoutComputeContext()
    {
        Assert.IsFalse(TryBuild(
            [
                SlotSource,
                HostSource("""
                        [ComputePipeline]
                        private void Run(int value)
                        {
                        }
                    """)
            ],
            "HostContractModelNoContextTests"));
    }

    [TestMethod]
    public void RejectsPipelineWithInvalidResourceParameter()
    {
        Assert.IsFalse(TryBuild(
            [
                SlotSource,
                HostSource("""
                        [ComputePipeline]
                        private void Run(in ComputeContext context, [ComputeResource(ComputeResourceAccess.Read)] int value)
                        {
                        }
                    """)
            ],
            "HostContractModelInvalidParameterTests"));
    }

    [TestMethod]
    public void RejectsHostWhenOneResourceIsInvalid()
    {
        Assert.IsFalse(TryBuild(
            [
                """
                using ComputeSharp;

                namespace Ukiyoe;

                public sealed partial class Host
                {
                    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                    private readonly ComputeResourceSlot<ReadWriteTexture3D<int>> volume = new();
                }
                """,
                HostSource("""
                        [ComputePipeline]
                        private void Run(in ComputeContext context)
                        {
                        }
                    """)
            ],
            "HostContractModelInvalidResourceTests"));
    }
}
