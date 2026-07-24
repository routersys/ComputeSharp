using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PipelineDescriptorReaderTests
{
    private static byte[] HostPayload()
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
            0x01, 0x00, 0x00, 0x00, 0x53,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
    }

    private static byte[] HostHash()
    {
        return
        [
            0xEC, 0x7B, 0x39, 0x96, 0x8B, 0x03, 0xC5, 0xC5, 0x7D, 0xDE, 0xE5, 0xB5, 0x94, 0x86, 0x0B, 0xAB,
            0x40, 0x0E, 0x70, 0x53, 0x74, 0xBC, 0xAE, 0x8E, 0xE6, 0x20, 0xA6, 0xE8, 0x94, 0x98, 0x9F, 0xF2
        ];
    }

    private static byte[] SetPayload()
    {
        return
        [
            0x01,
            0x01, 0x00, 0x00, 0x00, 0x52,
            0x01, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x4D,
            0x01, 0x00, 0x00, 0x00, 0x54,
            0x00, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01
        ];
    }

    private static byte[] SetHash()
    {
        return
        [
            0xED, 0xD2, 0x68, 0x93, 0xE9, 0xF6, 0x4D, 0x68, 0x1B, 0x19, 0xA2, 0xEC, 0x5B, 0x09, 0x7E, 0x7A,
            0x82, 0xD2, 0x1F, 0x08, 0x2F, 0x91, 0x0A, 0xF6, 0xAB, 0xAF, 0x9C, 0x3E, 0x73, 0x38, 0xDB, 0x15
        ];
    }

    private static byte[] BuildDescriptor(byte[] payload, byte[] hash)
    {
        byte[] descriptor = new byte[48 + payload.Length];
        ReadOnlySpan<byte> header = [0x43, 0x53, 0x50, 0x31, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00];

        header.CopyTo(descriptor);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12, 4), (uint)payload.Length);
        hash.CopyTo(descriptor, 16);
        payload.CopyTo(descriptor, 48);

        return descriptor;
    }

    private static byte[] Assemble(byte[] payload)
    {
        ReadOnlySpan<byte> header = [0x43, 0x53, 0x50, 0x31, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00];
        byte[] hashInput = new byte[10 + payload.Length];

        header.CopyTo(hashInput);
        payload.CopyTo(hashInput, 10);

        return BuildDescriptor(payload, SHA256.HashData(hashInput));
    }

    private static byte[] MutatedHost(int index, byte value)
    {
        byte[] descriptor = BuildDescriptor(HostPayload(), HostHash());

        descriptor[index] = value;

        return descriptor;
    }

    private static byte[] MutatedHostPayload(int index, byte value)
    {
        byte[] payload = HostPayload();

        payload[index] = value;

        return Assemble(payload);
    }

    [TestMethod]
    public void ReadsMinimalPipelineHost()
    {
        PipelineDescriptorSet set = PipelineDescriptorReader.Read(BuildDescriptor(HostPayload(), HostHash()));

        Assert.AreEqual(DescriptorKind.PipelineHost, set.Kind);

        PipelineHostDescriptor host = set.Host;

        Assert.AreEqual(new PipelineSchemaVersion(1, 0, 1), host.Schema);
        Assert.AreEqual("H", host.HostTypeMetadataName);
        Assert.AreEqual(1, host.MaximumConcurrentInvocations);
        Assert.AreEqual(0, host.Structural.MaximumTrackedResourceCount);
        Assert.AreEqual(1, host.Structural.MaximumCommandListSegments);
        Assert.AreEqual(0, host.Structural.OwnedSlotCount);
        Assert.AreEqual(1, host.Pipelines.Length);
        Assert.AreEqual(0, host.Slots.Length);

        PipelineDescriptor pipeline = host.Pipelines.Span[0];

        Assert.AreEqual(0u, pipeline.Ordinal.Value);
        Assert.AreEqual("M", pipeline.MethodMetadataName);
        Assert.AreEqual("S", pipeline.CanonicalSignature);
        Assert.AreEqual(PipelineFlags.None, pipeline.Flags);
        Assert.AreEqual(0, pipeline.MaximumTrackedResourceCount);
        Assert.AreEqual(1, pipeline.MaximumCommandListSegments);
        Assert.AreEqual(0, pipeline.Parameters.Length);
        Assert.AreEqual(0, pipeline.InternalResources.Length);
    }

    [TestMethod]
    public void ReadsMinimalInteropResourceSet()
    {
        PipelineDescriptorSet set = PipelineDescriptorReader.Read(BuildDescriptor(SetPayload(), SetHash()));

        Assert.AreEqual(DescriptorKind.InteropResourceSet, set.Kind);

        InteropResourceSetDescriptor resourceSet = set.ResourceSet;

        Assert.AreEqual("R", resourceSet.ResourceSetTypeMetadataName);
        Assert.AreEqual(1, resourceSet.Structural.SharedTextureSlotCount);
        Assert.AreEqual(1, resourceSet.SharedTextures.Length);

        SharedTextureContractDescriptor sharedTexture = resourceSet.SharedTextures.Span[0];

        Assert.AreEqual(0u, sharedTexture.Ordinal.Value);
        Assert.AreEqual("M", sharedTexture.MemberMetadataName);
        Assert.AreEqual("T", sharedTexture.ResourceTypeMetadataName);
        Assert.AreEqual(ComputeResourceResizePolicy.Exact, sharedTexture.ResizePolicy);
        Assert.AreEqual(ComputeResourceAccess.ReadWrite, sharedTexture.ComputeAccess);
        Assert.AreEqual(ExternalResourceAccess.Write, sharedTexture.ExternalAccess);
        Assert.AreEqual(ExternalTextureUsage.RenderTarget, sharedTexture.ExternalUsage);
        Assert.AreEqual(ComputeAlphaMode.Premultiplied, sharedTexture.AlphaMode);
        Assert.AreEqual(ComputeSharedTextureInitialOwner.External, sharedTexture.InitialOwner);
        Assert.AreEqual(ComputeResourceRecovery.RecreateFromHost, sharedTexture.Recovery);
    }

    [TestMethod]
    public void RejectsTooShortForHeader()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(new byte[10]));
    }

    [TestMethod]
    public void RejectsCorruptedMagic()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(0, 0x00)));
    }

    [TestMethod]
    public void RejectsSchemaMajorMismatch()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(4, 0x02)));
    }

    [TestMethod]
    public void RejectsDescriptorFormatMismatch()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(8, 0x02)));
    }

    [TestMethod]
    public void RejectsNonZeroReserved()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(10, 0x01)));
    }

    [TestMethod]
    public void RejectsPayloadLengthMismatch()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(12, 0x41)));
    }

    [TestMethod]
    public void RejectsContractHashMismatch()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHost(16, 0x00)));
    }

    [TestMethod]
    public void RejectsTruncatedDescriptor()
    {
        byte[] descriptor = BuildDescriptor(HostPayload(), HostHash());

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor.AsSpan(0, descriptor.Length - 1).ToArray()));
    }

    [TestMethod]
    public void RejectsUnknownDescriptorKind()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Assemble([0x02])));
    }

    [TestMethod]
    public void RejectsUnknownEnumValue()
    {
        byte[] payload = SetPayload();

        payload[29] = 0x05;

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Assemble(payload)));
    }

    [TestMethod]
    public void RejectsUnknownPipelineFlag()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHostPayload(40, 0x04)));
    }

    [TestMethod]
    public void RejectsPipelineOrdinalGap()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHostPayload(26, 0x01)));
    }

    [TestMethod]
    public void RejectsEmptyPipelineTable()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHostPayload(22, 0x00)));
    }

    [TestMethod]
    public void RejectsMaximumConcurrentInvocationsBelowOne()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHostPayload(6, 0x00)));
    }

    [TestMethod]
    public void RejectsStructuralSegmentsMismatch()
    {
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(MutatedHostPayload(14, 0x02)));
    }

    [TestMethod]
    public void RejectsOversizedTableCount()
    {
        byte[] payload = HostPayload();

        payload[22] = 0xFF;
        payload[23] = 0xFF;
        payload[24] = 0xFF;
        payload[25] = 0xFF;

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Assemble(payload)));
    }
}
