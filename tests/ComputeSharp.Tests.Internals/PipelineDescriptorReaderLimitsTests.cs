using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PipelineDescriptorReaderLimitsTests
{
    private sealed class PayloadBuilder
    {
        private readonly List<byte> bytes = [];

        public PayloadBuilder Byte(byte value)
        {
            this.bytes.Add(value);

            return this;
        }

        public PayloadBuilder UInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);

            this.bytes.AddRange(buffer.ToArray());

            return this;
        }

        public PayloadBuilder Text(string value)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(value);

            _ = UInt32((uint)encoded.Length);

            this.bytes.AddRange(encoded);

            return this;
        }

        public PayloadBuilder RawText(uint declaredLength, byte[] content)
        {
            _ = UInt32(declaredLength);

            this.bytes.AddRange(content);

            return this;
        }

        public PayloadBuilder Fill(int count)
        {
            this.bytes.AddRange(new byte[count]);

            return this;
        }

        public byte[] ToArray()
        {
            return [.. this.bytes];
        }
    }

    private static byte[] Descriptor(byte[] payload, ushort major = 1, ushort minor = 0, ushort descriptorFormat = 1)
    {
        byte[] descriptor = new byte[48 + payload.Length];
        ReadOnlySpan<byte> magic = [0x43, 0x53, 0x50, 0x31];

        magic.CopyTo(descriptor);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(4, 2), major);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(6, 2), minor);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(8, 2), descriptorFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12, 4), (uint)payload.Length);
        payload.CopyTo(descriptor, 48);

        byte[] hashInput = new byte[10 + payload.Length];

        descriptor.AsSpan(0, 10).CopyTo(hashInput);
        payload.CopyTo(hashInput, 10);
        SHA256.HashData(hashInput).CopyTo(descriptor, 16);

        return descriptor;
    }

    private static void WritePipeline(PayloadBuilder builder, int ordinal, int parameterCount, int internalCount, string signature)
    {
        int trackedCount = parameterCount + internalCount;

        _ = builder
            .UInt32((uint)ordinal)
            .Text("M")
            .Text(signature)
            .UInt32(0)
            .UInt32((uint)trackedCount)
            .UInt32((uint)(trackedCount > 0 ? 2 : 1))
            .UInt32((uint)parameterCount);

        for (int i = 0; i < parameterCount; i++)
        {
            WriteResource(builder, i);
        }

        _ = builder.UInt32((uint)internalCount);

        for (int i = 0; i < internalCount; i++)
        {
            WriteResource(builder, parameterCount + i);
        }
    }

    private static void WriteResource(PayloadBuilder builder, int ordinal, byte hasSlot = 0)
    {
        _ = builder
            .UInt32((uint)ordinal)
            .Text("T")
            .Byte(0)
            .Byte(0)
            .Byte(0)
            .Byte(0)
            .Byte(hasSlot)
            .UInt32(0)
            .UInt32(0);
    }

    private static void WriteSlot(PayloadBuilder builder, int ordinal)
    {
        _ = builder
            .UInt32((uint)ordinal)
            .Text("S")
            .Text("B")
            .Byte(1)
            .Byte(0)
            .Byte(0)
            .UInt32(1)
            .UInt32(0)
            .UInt32(0)
            .Text("S")
            .Text("B")
            .Text("L")
            .Byte(0);
    }

    private static byte[] HostPayload(int pipelineCount, int parameterCount, int internalCount, int slotCount)
    {
        int trackedCount = parameterCount + internalCount;
        PayloadBuilder builder = new();

        _ = builder
            .Byte(0)
            .Text("H")
            .UInt32(1)
            .UInt32((uint)trackedCount)
            .UInt32((uint)(trackedCount > 0 ? 2 : 1))
            .UInt32((uint)slotCount)
            .UInt32((uint)pipelineCount);

        for (int i = 0; i < pipelineCount; i++)
        {
            WritePipeline(builder, i, parameterCount, internalCount, Signature(69));
        }

        _ = builder.UInt32((uint)slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            WriteSlot(builder, i);
        }

        return builder.ToArray();
    }

    private static byte[] PaddedHostPayload(int[] signatureLengths)
    {
        PayloadBuilder builder = new();

        _ = builder
            .Byte(0)
            .Text("H")
            .UInt32(1)
            .UInt32(0)
            .UInt32(1)
            .UInt32(0)
            .UInt32((uint)signatureLengths.Length);

        for (int i = 0; i < signatureLengths.Length; i++)
        {
            WritePipeline(builder, i, 0, 0, Signature(signatureLengths[i]));
        }

        return builder.UInt32(0).ToArray();
    }

    private static byte[] SharedTexturePayload(int count)
    {
        PayloadBuilder builder = new();

        _ = builder
            .Byte(1)
            .Text("R")
            .UInt32((uint)count)
            .UInt32((uint)count);

        for (int i = 0; i < count; i++)
        {
            _ = builder
                .UInt32((uint)i)
                .Text("M")
                .Text("T")
                .Byte(0)
                .Byte(2)
                .Byte(1)
                .Byte(1)
                .Byte(1)
                .Byte(1)
                .Byte(1);
        }

        return builder.ToArray();
    }

    private static byte[] HostPayloadWithResource(byte sharing, byte ownership, byte hasSlot, uint slot, uint slotResourceIndex)
    {
        PayloadBuilder builder = new();

        _ = builder
            .Byte(0)
            .Text("H")
            .UInt32(1)
            .UInt32(1)
            .UInt32(2)
            .UInt32(0)
            .UInt32(1)
            .UInt32(0)
            .Text("M")
            .Text(Signature(69))
            .UInt32(0)
            .UInt32(1)
            .UInt32(2)
            .UInt32(1)
            .UInt32(0)
            .Text("T")
            .Byte(0)
            .Byte(sharing)
            .Byte(0)
            .Byte(ownership)
            .Byte(hasSlot)
            .UInt32(slot)
            .UInt32(slotResourceIndex)
            .UInt32(0)
            .UInt32(0);

        return builder.ToArray();
    }

    private static string Signature(int totalLength)
    {
        const string Prefix = "H|M|00000000|System.Void|00000002|03:ComputeSharp.ComputeContext|00:";

        return Prefix + new string('T', totalLength - Prefix.Length);
    }

    private static int[] SignatureLengthsForTotalLength(int totalLength)
    {
        int budget = totalLength - 78 - (33 * 16);
        int[] lengths = new int[16];

        for (int i = 0; i < 15; i++)
        {
            lengths[i] = PipelineDescriptorLimits.MaximumStringUtf8ByteLength;
            budget -= PipelineDescriptorLimits.MaximumStringUtf8ByteLength;
        }

        lengths[15] = budget;

        return lengths;
    }

    [TestMethod]
    public void AcceptsDescriptorAtMaximumByteLength()
    {
        byte[] descriptor = Descriptor(PaddedHostPayload(SignatureLengthsForTotalLength(PipelineDescriptorLimits.MaximumDescriptorByteLength)));

        Assert.AreEqual(PipelineDescriptorLimits.MaximumDescriptorByteLength, descriptor.Length);
        Assert.AreEqual(DescriptorKind.PipelineHost, PipelineDescriptorReader.Read(descriptor).Kind);
    }

    [TestMethod]
    public void RejectsDescriptorAboveMaximumByteLength()
    {
        byte[] descriptor = Descriptor(PaddedHostPayload(SignatureLengthsForTotalLength(PipelineDescriptorLimits.MaximumDescriptorByteLength + 1)));

        Assert.AreEqual(PipelineDescriptorLimits.MaximumDescriptorByteLength + 1, descriptor.Length);
        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void AcceptsStringAtMaximumByteLength()
    {
        byte[] descriptor = Descriptor(PaddedHostPayload([PipelineDescriptorLimits.MaximumStringUtf8ByteLength]));

        Assert.AreEqual(DescriptorKind.PipelineHost, PipelineDescriptorReader.Read(descriptor).Kind);
    }

    [TestMethod]
    public void RejectsStringAboveMaximumByteLength()
    {
        byte[] descriptor = Descriptor(PaddedHostPayload([PipelineDescriptorLimits.MaximumStringUtf8ByteLength + 1]));

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void AcceptsResourcesAtMaximumPerPipeline()
    {
        byte[] descriptor = Descriptor(HostPayload(1, 128, 128, 0));

        Assert.AreEqual(256, PipelineDescriptorReader.Read(descriptor).Host.Pipelines.Span[0].MaximumTrackedResourceCount);
    }

    [TestMethod]
    public void RejectsResourcesAboveMaximumPerPipeline()
    {
        byte[] descriptor = Descriptor(HostPayload(1, 129, 128, 0));

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void AcceptsPipelineCountAtMaximum()
    {
        byte[] descriptor = Descriptor(HostPayload(PipelineDescriptorLimits.MaximumPipelineCount, 0, 0, 0));

        Assert.AreEqual(PipelineDescriptorLimits.MaximumPipelineCount, PipelineDescriptorReader.Read(descriptor).Host.Pipelines.Length);
    }

    [TestMethod]
    public void RejectsPipelineCountAboveMaximum()
    {
        byte[] descriptor = Descriptor(HostPayload(PipelineDescriptorLimits.MaximumPipelineCount + 1, 0, 0, 0));

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void AcceptsSlotCountAtMaximum()
    {
        byte[] descriptor = Descriptor(HostPayload(1, 0, 0, PipelineDescriptorLimits.MaximumSlotCount));

        Assert.AreEqual(PipelineDescriptorLimits.MaximumSlotCount, PipelineDescriptorReader.Read(descriptor).Host.Slots.Length);
    }

    [TestMethod]
    public void RejectsSlotCountAboveMaximum()
    {
        byte[] descriptor = Descriptor(HostPayload(1, 0, 0, PipelineDescriptorLimits.MaximumSlotCount + 1));

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void AcceptsSharedTextureCountAtMaximum()
    {
        byte[] descriptor = Descriptor(SharedTexturePayload(PipelineDescriptorLimits.MaximumSharedTextureCount));

        Assert.AreEqual(PipelineDescriptorLimits.MaximumSharedTextureCount, PipelineDescriptorReader.Read(descriptor).ResourceSet.SharedTextures.Length);
    }

    [TestMethod]
    public void RejectsSharedTextureCountAboveMaximum()
    {
        byte[] descriptor = Descriptor(SharedTexturePayload(PipelineDescriptorLimits.MaximumSharedTextureCount + 1));

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void RejectsCountBelowMinimumElementByteLength()
    {
        PayloadBuilder builder = new();

        byte[] payload = builder
            .Byte(0)
            .Text("H")
            .UInt32(1)
            .UInt32(0)
            .UInt32(1)
            .UInt32(0)
            .UInt32(50)
            .Fill(100)
            .ToArray();

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsSchemaMinorMismatch()
    {
        byte[] descriptor = Descriptor(HostPayload(1, 0, 0, 0), minor: 1);

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(descriptor));
    }

    [TestMethod]
    public void RejectsInvalidUtf8String()
    {
        PayloadBuilder builder = new();

        byte[] payload = builder
            .Byte(0)
            .RawText(2, [0xFF, 0xFE])
            .ToArray();

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsNonNormalizedString()
    {
        PayloadBuilder builder = new();

        byte[] payload = builder
            .Byte(0)
            .RawText(3, [0x65, 0xCC, 0x81])
            .ToArray();

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsNullStringMarker()
    {
        PayloadBuilder builder = new();

        byte[] payload = builder
            .Byte(0)
            .UInt32(0xFFFFFFFFu)
            .ToArray();

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsTrailingBytesWithValidHash()
    {
        byte[] payload = [.. HostPayload(1, 0, 0, 0), 0x00, 0x00, 0x00, 0x00, 0x00];

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsInvalidBooleanEncoding()
    {
        PayloadBuilder builder = new();

        _ = builder
            .Byte(0)
            .Text("H")
            .UInt32(1)
            .UInt32(1)
            .UInt32(2)
            .UInt32(0)
            .UInt32(1)
            .UInt32(0)
            .Text("M")
            .Text("S")
            .UInt32(0)
            .UInt32(1)
            .UInt32(2)
            .UInt32(1);

        WriteResource(builder, 0, hasSlot: 2);

        byte[] payload = builder.UInt32(0).UInt32(0).ToArray();

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsSharedTextureSlotOwnershipInResourceContract()
    {
        byte[] payload = HostPayloadWithResource(1, 3, 1, 0, 0);

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void RejectsExternalSharingBoundToSlot()
    {
        byte[] payload = HostPayloadWithResource(1, 1, 1, 0, 0);

        _ = Assert.ThrowsException<InvalidDataException>(() => PipelineDescriptorReader.Read(Descriptor(payload)));
    }

    [TestMethod]
    public void AcceptsExternalSharingWithoutSlot()
    {
        byte[] payload = HostPayloadWithResource(1, 0, 0, 0, 0);

        Assert.AreEqual(DescriptorKind.PipelineHost, PipelineDescriptorReader.Read(Descriptor(payload)).Kind);
    }
}
