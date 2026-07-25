extern alias runtime;

using System;
using System.Security.Cryptography;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RuntimeDescriptorKind = runtime::ComputeSharp.Graphics.Pipelines.DescriptorKind;
using RuntimeDescriptorReader = runtime::ComputeSharp.Graphics.Pipelines.PipelineDescriptorReader;
using RuntimeDescriptorSet = runtime::ComputeSharp.Graphics.Pipelines.PipelineDescriptorSet;
using RuntimeHostDescriptor = runtime::ComputeSharp.Graphics.Pipelines.PipelineHostDescriptor;
using RuntimeOwnedSlotDescriptor = runtime::ComputeSharp.Graphics.Pipelines.OwnedSlotDescriptor;
using RuntimePipelineDescriptor = runtime::ComputeSharp.Graphics.Pipelines.PipelineDescriptor;
using RuntimeResourceContractDescriptor = runtime::ComputeSharp.Graphics.Pipelines.ResourceContractDescriptor;
using RuntimeResourceSetDescriptor = runtime::ComputeSharp.Graphics.Pipelines.InteropResourceSetDescriptor;
using RuntimeSharedTextureDescriptor = runtime::ComputeSharp.Graphics.Pipelines.SharedTextureContractDescriptor;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class PipelineDescriptorWriterTests
{
    private const string MinimalHostSource = """
        using ComputeSharp;

        [ComputePipelineHost("device", 1)]
        public sealed partial class H
        {
            private readonly GraphicsDevice device = null!;

            [ComputePipeline]
            private void M(in ComputeContext context)
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
                ComputeResourceResizePolicy.GrowOnly,
                ComputeResourceAccess.ReadWrite,
                ExternalResourceAccess.Read,
                ExternalTextureUsage.Sampled,
                ComputeAlphaMode.Premultiplied,
                ComputeSharedTextureInitialOwner.Compute,
                ComputeResourceRecovery.Recompute)]
            private readonly SharedTextureSlot<Bgra32, Float4, ExternalView> output;

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

    private static byte[] MinimalHostPayload()
    {
        return
        [
            0x00,
            0x01, 0x00, 0x00, 0x00, 0x48,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x4D,
            0x40, 0x00, 0x00, 0x00,
            0x48, 0x7C, 0x4D, 0x7C, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x7C, 0x53, 0x79, 0x73,
            0x74, 0x65, 0x6D, 0x2E, 0x56, 0x6F, 0x69, 0x64, 0x7C, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
            0x31, 0x7C, 0x30, 0x33, 0x3A, 0x43, 0x6F, 0x6D, 0x70, 0x75, 0x74, 0x65, 0x53, 0x68, 0x61, 0x72,
            0x70, 0x2E, 0x43, 0x6F, 0x6D, 0x70, 0x75, 0x74, 0x65, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x78, 0x74,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
    }

    private static byte[] MinimalHostHash()
    {
        return
        [
            0xB6, 0xCE, 0xB2, 0xD2, 0xC9, 0x29, 0x6C, 0x2D, 0x58, 0x90, 0xFB, 0xC7, 0xFE, 0xAD, 0x57, 0xAD,
            0xDD, 0x8A, 0x64, 0x4D, 0x02, 0xE0, 0x4C, 0xB9, 0x1C, 0x25, 0x22, 0x10, 0x67, 0x63, 0x57, 0x54
        ];
    }

    private static byte[] BuildGoldenDescriptor(byte[] payload, byte[] hash)
    {
        byte[] descriptor = new byte[48 + payload.Length];
        ReadOnlySpan<byte> header = [0x43, 0x53, 0x50, 0x31, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00];

        header.CopyTo(descriptor);
        descriptor[12] = (byte)(payload.Length & 0xFF);
        descriptor[13] = (byte)((payload.Length >> 8) & 0xFF);
        descriptor[14] = (byte)((payload.Length >> 16) & 0xFF);
        descriptor[15] = (byte)((payload.Length >> 24) & 0xFF);
        hash.CopyTo(descriptor, 16);
        payload.CopyTo(descriptor, 48);

        return descriptor;
    }

    private static PipelineHostContractInfo BuildHost(string[] sources, string assemblyName, string hostTypeMetadataName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol hostSymbol = compilation.GetTypeByMetadataName(hostTypeMetadataName)!;

        Assert.IsNotNull(hostSymbol);
        Assert.IsTrue(PipelineHostContractModelBuilder.TryBuild(hostSymbol, symbols, out PipelineHostContractInfo host));

        return host;
    }

    private static InteropResourceSetContractInfo BuildResourceSet(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, assemblyName);

        Assert.IsTrue(PipelineWellKnownSymbols.TryCreate(compilation, out PipelineWellKnownSymbols? symbols));

        INamedTypeSymbol resourceSetSymbol = compilation.GetTypeByMetadataName("Ukiyoe.ResourceSet")!;

        Assert.IsNotNull(resourceSetSymbol);
        Assert.IsTrue(InteropResourceSetContractModelBuilder.TryBuild(resourceSetSymbol, symbols, out InteropResourceSetContractInfo resourceSet));

        return resourceSet;
    }

    [TestMethod]
    public void WritesGoldenMinimalHostDescriptor()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterGoldenHostTests", "H"));

        CollectionAssert.AreEqual(BuildGoldenDescriptor(MinimalHostPayload(), MinimalHostHash()), descriptor);
    }

    [TestMethod]
    public void ExcludesReservedAndPayloadLengthFromHash()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterHashScopeTests", "H"));
        byte[] hashInput = new byte[10 + descriptor.Length - 48];

        descriptor.AsSpan(0, 10).CopyTo(hashInput);
        descriptor.AsSpan(48).CopyTo(hashInput.AsSpan(10));

        CollectionAssert.AreEqual(SHA256.HashData(hashInput), descriptor[16..48]);
        CollectionAssert.AreEqual(MinimalHostHash(), descriptor[16..48]);
    }

    [TestMethod]
    public void ReadsWrittenMinimalHostDescriptor()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterRoundTripHostTests", "H"));
        RuntimeDescriptorSet set = RuntimeDescriptorReader.Read(descriptor);

        Assert.AreEqual(RuntimeDescriptorKind.PipelineHost, set.Kind);

        RuntimeHostDescriptor host = set.Host;

        Assert.AreEqual("H", host.HostTypeMetadataName);
        Assert.AreEqual(1, host.MaximumConcurrentInvocations);
        Assert.AreEqual(0, host.Structural.MaximumTrackedResourceCount);
        Assert.AreEqual(1, host.Structural.MaximumCommandListSegments);
        Assert.AreEqual(0, host.Structural.OwnedSlotCount);
        Assert.AreEqual(1, host.Pipelines.Length);
        Assert.AreEqual(0, host.Slots.Length);

        RuntimePipelineDescriptor pipeline = host.Pipelines.Span[0];

        Assert.AreEqual(0u, pipeline.Ordinal.Value);
        Assert.AreEqual("M", pipeline.MethodMetadataName);
        Assert.AreEqual("H|M|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext", pipeline.CanonicalSignature);
    }

    [TestMethod]
    public void ReadsWrittenHostDescriptorWithSlotsAndResources()
    {
        const string HostSource = """
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class Grid
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> ColorB { get; } = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> ColorA { get; } = null!;
            }

            [ComputePipelineHost("device", 2)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
                private readonly ComputeResourceSlot<ReadWriteTexture2D<Bgra32, Float4>> output = new();

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Grid> grid = new();

                [ComputePipelineResource(ComputeResourceAccess.Read)]
                private readonly ReadOnlyBuffer<float> weights = null!;

                [ComputeInterop]
                [ComputePipeline]
                private void Run(
                    in ComputeContext context,
                    [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> shared)
                {
                }
            }
            """;

        PipelineHostContractInfo model = BuildHost([HostSource], "WriterRoundTripFullHostTests", "Ukiyoe.Host");
        byte[] descriptor = PipelineDescriptorWriter.Write(model);
        RuntimeHostDescriptor host = RuntimeDescriptorReader.Read(descriptor).Host;

        Assert.AreEqual("Ukiyoe.Host", host.HostTypeMetadataName);
        Assert.AreEqual(2, host.MaximumConcurrentInvocations);
        Assert.AreEqual(2, host.Slots.Length);

        RuntimeOwnedSlotDescriptor groupSlot = host.Slots.Span[0];
        RuntimeOwnedSlotDescriptor outputSlot = host.Slots.Span[1];

        Assert.AreEqual("grid", groupSlot.MemberMetadataName);
        Assert.AreEqual(0u, groupSlot.Ordinal.Value);
        Assert.AreEqual(2, groupSlot.PlanFields.Length);
        Assert.AreEqual("output", outputSlot.MemberMetadataName);
        Assert.AreEqual(1u, outputSlot.Ordinal.Value);
        Assert.AreEqual(2, outputSlot.PlanFields.Length);

        RuntimePipelineDescriptor pipeline = host.Pipelines.Span[0];

        Assert.AreEqual("Run", pipeline.MethodMetadataName);
        Assert.AreEqual(1, pipeline.Parameters.Length);
        Assert.AreEqual(4, pipeline.InternalResources.Length);
        Assert.AreEqual(5, pipeline.MaximumTrackedResourceCount);
        Assert.AreEqual(3, pipeline.MaximumCommandListSegments);

        RuntimeResourceContractDescriptor parameter = pipeline.Parameters.Span[0];

        Assert.AreEqual(0u, parameter.Ordinal.Value);
        Assert.IsFalse(parameter.HasSlot);

        Assert.AreEqual(1u, pipeline.InternalResources.Span[0].Ordinal.Value);
        Assert.AreEqual(4u, pipeline.InternalResources.Span[3].Ordinal.Value);
    }

    [TestMethod]
    public void ReadsWrittenResourceSetDescriptor()
    {
        InteropResourceSetContractInfo model = BuildResourceSet(ResourceSetSource, "WriterRoundTripResourceSetTests");
        byte[] descriptor = PipelineDescriptorWriter.Write(model);
        RuntimeDescriptorSet set = RuntimeDescriptorReader.Read(descriptor);

        Assert.AreEqual(RuntimeDescriptorKind.InteropResourceSet, set.Kind);

        RuntimeResourceSetDescriptor resourceSet = set.ResourceSet;

        Assert.AreEqual("Ukiyoe.ResourceSet", resourceSet.ResourceSetTypeMetadataName);
        Assert.AreEqual(2, resourceSet.Structural.SharedTextureSlotCount);
        Assert.AreEqual(2, resourceSet.SharedTextures.Length);

        RuntimeSharedTextureDescriptor output = resourceSet.SharedTextures.Span[0];
        RuntimeSharedTextureDescriptor source = resourceSet.SharedTextures.Span[1];

        Assert.AreEqual("output", output.MemberMetadataName);
        Assert.AreEqual(0u, output.Ordinal.Value);
        Assert.AreEqual("ComputeSharp.ReadWriteTexture2D`2[ComputeSharp.Bgra32,ComputeSharp.Float4]", output.ResourceTypeMetadataName);
        Assert.AreEqual("source", source.MemberMetadataName);
        Assert.AreEqual(1u, source.Ordinal.Value);
    }

    [TestMethod]
    public void ProducesIdenticalBytesForReversedSyntaxTreeOrder()
    {
        const string GroupSource = """
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            public sealed partial class Grid
            {
                [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
                public ReadWriteBuffer<int> ColorA { get; } = null!;
            }
            """;

        const string HostSource = """
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputePipelineHost("device", 1)]
            public sealed partial class Host
            {
                private readonly GraphicsDevice device = null!;

                [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
                private readonly ComputeResourceGroupSlot<Grid> grid = new();

                [ComputePipeline]
                private void Run(in ComputeContext context)
                {
                }
            }
            """;

        byte[] forward = PipelineDescriptorWriter.Write(BuildHost([GroupSource, HostSource], "WriterForwardTests", "Ukiyoe.Host"));
        byte[] reversed = PipelineDescriptorWriter.Write(BuildHost([HostSource, GroupSource], "WriterReversedTests", "Ukiyoe.Host"));

        CollectionAssert.AreEqual(forward, reversed);
    }

    [TestMethod]
    public void RejectsCorruptedPayloadByte()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterCorruptionTests", "H"));

        descriptor[48] = 0x01;

        _ = Assert.ThrowsException<System.IO.InvalidDataException>(() => RuntimeDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void RejectsCorruptedReservedField()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterReservedTests", "H"));

        descriptor[10] = 0x01;

        _ = Assert.ThrowsException<System.IO.InvalidDataException>(() => RuntimeDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void RejectsCorruptedPayloadLength()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterPayloadLengthTests", "H"));

        descriptor[12] = (byte)(descriptor[12] + 1);

        _ = Assert.ThrowsException<System.IO.InvalidDataException>(() => RuntimeDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void RejectsCorruptedContractHash()
    {
        byte[] descriptor = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterHashTests", "H"));

        descriptor[16] = (byte)(descriptor[16] ^ 0xFF);

        _ = Assert.ThrowsException<System.IO.InvalidDataException>(() => RuntimeDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void ProducesIdenticalBytesForRepeatedCompilations()
    {
        byte[] first = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterRepeatFirstTests", "H"));
        byte[] second = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterRepeatSecondTests", "H"));
        byte[] third = PipelineDescriptorWriter.Write(BuildHost([MinimalHostSource], "WriterRepeatThirdTests", "H"));

        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreEqual(second, third);
    }

    [TestMethod]
    public void ProducesIdenticalBytesForRepeatedWritesOfSameModel()
    {
        PipelineHostContractInfo host = BuildHost([MinimalHostSource], "WriterSameModelTests", "H");

        CollectionAssert.AreEqual(PipelineDescriptorWriter.Write(host), PipelineDescriptorWriter.Write(host));
    }

    [TestMethod]
    public void ProducesIdenticalResourceSetBytesForRepeatedCompilations()
    {
        byte[] first = PipelineDescriptorWriter.Write(BuildResourceSet(ResourceSetSource, "WriterResourceSetRepeatFirstTests"));
        byte[] second = PipelineDescriptorWriter.Write(BuildResourceSet(ResourceSetSource, "WriterResourceSetRepeatSecondTests"));

        CollectionAssert.AreEqual(first, second);
    }
}
