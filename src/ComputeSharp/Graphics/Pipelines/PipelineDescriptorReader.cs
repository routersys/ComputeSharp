using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ComputeSharp.Graphics.Pipelines;

internal static class PipelineDescriptorReader
{
    private const int HeaderSize = 48;

    private const uint NullStringMarker = 0xFFFFFFFFu;

    private const int LengthDimensionMask = 1 << (int)ResourcePlanDimensionKind.Length;

    private const int Texture2DDimensionMask = (1 << (int)ResourcePlanDimensionKind.Width) | (1 << (int)ResourcePlanDimensionKind.Height);

    public static PipelineDescriptorSet Read(ReadOnlySpan<byte> descriptor)
    {
        if (descriptor.Length is < HeaderSize or > PipelineDescriptorLimits.MaximumDescriptorByteLength)
        {
            throw Invalid();
        }

        if (!descriptor.Slice(0, 4).SequenceEqual("CSP1"u8))
        {
            throw Invalid();
        }

        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(4, 2));
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(6, 2));
        ushort descriptorFormat = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(8, 2));

        if (major != PipelineSchema.Major || minor != PipelineSchema.Minor || descriptorFormat != PipelineSchema.DescriptorFormat)
        {
            throw Invalid();
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(10, 2)) != 0)
        {
            throw Invalid();
        }

        uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Slice(12, 4));

        if (payloadLength != (uint)(descriptor.Length - HeaderSize))
        {
            throw Invalid();
        }

        ReadOnlySpan<byte> contractHash = descriptor.Slice(16, 32);
        ReadOnlySpan<byte> payload = descriptor.Slice(HeaderSize);

        VerifyContractHash(descriptor.Slice(0, 10), payload, contractHash);

        PipelineSchemaVersion schema = new(major, minor, descriptorFormat);
        ContractHash256 hash = ReadContractHash256(contractHash);

        Csp1Reader reader = new(payload);

        DescriptorKind kind = (DescriptorKind)ReadEnumByte(ref reader, (byte)DescriptorKind.InteropResourceSet);

        return kind switch
        {
            DescriptorKind.PipelineHost => ReadPipelineHost(ref reader, schema, hash),
            _ => ReadInteropResourceSet(ref reader, schema, hash)
        };
    }

    private static PipelineDescriptorSet ReadPipelineHost(ref Csp1Reader reader, PipelineSchemaVersion schema, ContractHash256 hash)
    {
        string hostTypeMetadataName = ReadString(ref reader);
        int maximumConcurrentInvocations = ReadInt32(ref reader);

        if (maximumConcurrentInvocations < 1)
        {
            throw Invalid();
        }

        int structuralMaximumTrackedResourceCount = ReadInt32(ref reader);
        int structuralMaximumCommandListSegments = ReadInt32(ref reader);
        int structuralOwnedSlotCount = ReadInt32(ref reader);

        int pipelineCount = ReadCount(
            ref reader,
            PipelineDescriptorLimits.MaximumPipelineCount,
            PipelineDescriptorLimits.PipelineDescriptorMinimumByteLength);

        if (pipelineCount == 0)
        {
            throw Invalid();
        }

        List<ResourceContractDescriptor> resourceList = [];
        PipelineIntermediate[] pipelineIntermediates = new PipelineIntermediate[pipelineCount];
        int aggregateMaximumTrackedResourceCount = 0;
        int aggregateMaximumCommandListSegments = 0;

        for (int i = 0; i < pipelineCount; i++)
        {
            if (reader.ReadUInt32() != (uint)i)
            {
                throw Invalid();
            }

            string methodMetadataName = ReadString(ref reader);
            string canonicalSignature = ReadString(ref reader);
            PipelineFlags flags = ReadPipelineFlags(ref reader);
            int maximumTrackedResourceCount = ReadInt32(ref reader);
            int maximumCommandListSegments = ReadInt32(ref reader);

            int parameterStart = resourceList.Count;
            int parameterCount = ReadResourceContracts(ref reader, resourceList, 0, PipelineDescriptorLimits.MaximumResourcesPerPipeline);
            int internalStart = resourceList.Count;
            int internalCount = ReadResourceContracts(ref reader, resourceList, parameterCount, PipelineDescriptorLimits.MaximumResourcesPerPipeline - parameterCount);

            if (maximumTrackedResourceCount != checked(parameterCount + internalCount))
            {
                throw Invalid();
            }

            int expectedSegments = 1 + (maximumTrackedResourceCount > 0 ? 1 : 0) + ((flags & PipelineFlags.InteropRoundTrip) != 0 ? 1 : 0);

            if (maximumCommandListSegments != expectedSegments || maximumCommandListSegments is < 1 or > 3)
            {
                throw Invalid();
            }

            pipelineIntermediates[i] = new PipelineIntermediate(
                methodMetadataName,
                canonicalSignature,
                flags,
                maximumTrackedResourceCount,
                maximumCommandListSegments,
                parameterStart,
                parameterCount,
                internalStart,
                internalCount);

            aggregateMaximumTrackedResourceCount = Math.Max(aggregateMaximumTrackedResourceCount, maximumTrackedResourceCount);
            aggregateMaximumCommandListSegments = Math.Max(aggregateMaximumCommandListSegments, maximumCommandListSegments);
        }

        int slotCount = ReadCount(
            ref reader,
            PipelineDescriptorLimits.MaximumSlotCount,
            PipelineDescriptorLimits.OwnedSlotDescriptorMinimumByteLength);

        if (slotCount != structuralOwnedSlotCount)
        {
            throw Invalid();
        }

        List<ResourcePlanFieldDescriptor> planFieldList = [];
        SlotIntermediate[] slotIntermediates = new SlotIntermediate[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            if (reader.ReadUInt32() != (uint)i)
            {
                throw Invalid();
            }

            string memberMetadataName = ReadString(ref reader);
            string resourceTypeMetadataName = ReadString(ref reader);
            ResourceOwnershipKind ownership = (ResourceOwnershipKind)ReadEnumByte(ref reader, (byte)ResourceOwnershipKind.SharedTextureSlot);
            ResourcePlanKind planKind = (ResourcePlanKind)ReadEnumByte(ref reader, (byte)ResourcePlanKind.SharedTexture2D);
            ComputeResourceRecovery recovery = (ComputeResourceRecovery)ReadEnumByte(ref reader, (byte)ComputeResourceRecovery.CapacityOnly);

            ValidateSlotContract(ownership, planKind);

            int fieldStart = planFieldList.Count;
            int fieldCount = ReadPlanFields(ref reader, planFieldList);

            slotIntermediates[i] = new SlotIntermediate(
                memberMetadataName,
                resourceTypeMetadataName,
                ownership,
                planKind,
                recovery,
                fieldStart,
                fieldCount);
        }

        if (!reader.IsAtEnd)
        {
            throw Invalid();
        }

        if (structuralMaximumTrackedResourceCount != aggregateMaximumTrackedResourceCount ||
            structuralMaximumCommandListSegments != aggregateMaximumCommandListSegments)
        {
            throw Invalid();
        }

        ResourceContractDescriptor[] resources = [.. resourceList];
        ResourcePlanFieldDescriptor[] planFields = [.. planFieldList];

        OwnedSlotDescriptor[] slots = new OwnedSlotDescriptor[slotCount];
        uint[] slotResourceCounts = new uint[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            SlotIntermediate intermediate = slotIntermediates[i];

            slots[i] = new OwnedSlotDescriptor(
                new SlotOrdinal((uint)i),
                intermediate.MemberMetadataName,
                intermediate.ResourceTypeMetadataName,
                intermediate.Ownership,
                intermediate.PlanKind,
                intermediate.Recovery,
                planFields.AsMemory(intermediate.FieldStart, intermediate.FieldCount));

            slotResourceCounts[i] = ValidateSlotPlanFields(planFields, intermediate.FieldStart, intermediate.FieldCount, intermediate.PlanKind);
        }

        foreach (ResourceContractDescriptor resource in resources)
        {
            ValidateResourceSlotBinding(resource, slots, slotResourceCounts);
        }

        PipelineDescriptor[] pipelines = new PipelineDescriptor[pipelineCount];

        for (int i = 0; i < pipelineCount; i++)
        {
            PipelineIntermediate intermediate = pipelineIntermediates[i];

            PipelineCanonicalSignatureValidator.Validate(
                intermediate.CanonicalSignature,
                hostTypeMetadataName,
                intermediate.MethodMetadataName);

            pipelines[i] = new PipelineDescriptor(
                new PipelineOrdinal((uint)i),
                intermediate.MethodMetadataName,
                intermediate.CanonicalSignature,
                intermediate.Flags,
                intermediate.MaximumTrackedResourceCount,
                intermediate.MaximumCommandListSegments,
                resources.AsMemory(intermediate.ParameterStart, intermediate.ParameterCount),
                resources.AsMemory(intermediate.InternalStart, intermediate.InternalCount));
        }

        StaticStructuralRequirements structural = new(
            structuralMaximumTrackedResourceCount,
            structuralMaximumCommandListSegments,
            structuralOwnedSlotCount);

        PipelineHostDescriptor host = new(
            schema,
            hash,
            hostTypeMetadataName,
            maximumConcurrentInvocations,
            structural,
            pipelines.AsMemory(),
            slots.AsMemory());

        return new PipelineDescriptorSet(DescriptorKind.PipelineHost, host, default);
    }

    private static PipelineDescriptorSet ReadInteropResourceSet(ref Csp1Reader reader, PipelineSchemaVersion schema, ContractHash256 hash)
    {
        string resourceSetTypeMetadataName = ReadString(ref reader);
        int sharedTextureSlotCount = ReadInt32(ref reader);

        int count = ReadCount(
            ref reader,
            PipelineDescriptorLimits.MaximumSharedTextureCount,
            PipelineDescriptorLimits.SharedTextureContractDescriptorMinimumByteLength);

        if (count != sharedTextureSlotCount)
        {
            throw Invalid();
        }

        SharedTextureContractDescriptor[] sharedTextures = new SharedTextureContractDescriptor[count];

        for (int i = 0; i < count; i++)
        {
            if (reader.ReadUInt32() != (uint)i)
            {
                throw Invalid();
            }

            string memberMetadataName = ReadString(ref reader);
            string resourceTypeMetadataName = ReadString(ref reader);
            ComputeResourceResizePolicy resizePolicy = (ComputeResourceResizePolicy)ReadEnumByte(ref reader, (byte)ComputeResourceResizePolicy.GrowOnly);
            ComputeResourceAccess computeAccess = (ComputeResourceAccess)ReadEnumByte(ref reader, (byte)ComputeResourceAccess.ReadWrite);
            ExternalResourceAccess externalAccess = (ExternalResourceAccess)ReadEnumByte(ref reader, (byte)ExternalResourceAccess.ReadWrite);
            ExternalTextureUsage externalUsage = (ExternalTextureUsage)ReadEnumByte(ref reader, (byte)ExternalTextureUsage.RenderTarget);
            ComputeAlphaMode alphaMode = (ComputeAlphaMode)ReadEnumByte(ref reader, (byte)ComputeAlphaMode.Straight);
            ComputeSharedTextureInitialOwner initialOwner = (ComputeSharedTextureInitialOwner)ReadEnumByte(ref reader, (byte)ComputeSharedTextureInitialOwner.External);
            ComputeResourceRecovery recovery = (ComputeResourceRecovery)ReadEnumByte(ref reader, (byte)ComputeResourceRecovery.CapacityOnly);

            sharedTextures[i] = new SharedTextureContractDescriptor(
                new SlotOrdinal((uint)i),
                memberMetadataName,
                resourceTypeMetadataName,
                resizePolicy,
                computeAccess,
                externalAccess,
                externalUsage,
                alphaMode,
                initialOwner,
                recovery);
        }

        if (!reader.IsAtEnd)
        {
            throw Invalid();
        }

        ResourceSetStructuralRequirements structural = new(sharedTextureSlotCount);

        InteropResourceSetDescriptor resourceSet = new(
            schema,
            hash,
            resourceSetTypeMetadataName,
            structural,
            sharedTextures.AsMemory());

        return new PipelineDescriptorSet(DescriptorKind.InteropResourceSet, default, resourceSet);
    }

    private static int ReadResourceContracts(ref Csp1Reader reader, List<ResourceContractDescriptor> resourceList, int ordinalBase, int maximumCount)
    {
        int count = ReadCount(ref reader, maximumCount, PipelineDescriptorLimits.ResourceContractDescriptorMinimumByteLength);

        for (int i = 0; i < count; i++)
        {
            if (reader.ReadUInt32() != (uint)(ordinalBase + i))
            {
                throw Invalid();
            }

            string resourceTypeMetadataName = ReadString(ref reader);
            ComputeResourceAccess access = (ComputeResourceAccess)ReadEnumByte(ref reader, (byte)ComputeResourceAccess.ReadWrite);
            ComputeResourceSharing sharing = (ComputeResourceSharing)ReadEnumByte(ref reader, (byte)ComputeResourceSharing.External);
            ComputeResourceAliasing aliasing = (ComputeResourceAliasing)ReadEnumByte(ref reader, (byte)ComputeResourceAliasing.Allow);
            ResourceOwnershipKind ownership = (ResourceOwnershipKind)ReadEnumByte(ref reader, (byte)ResourceOwnershipKind.SharedTextureSlot);
            bool hasSlot = ReadBoolean(ref reader);
            uint slot = reader.ReadUInt32();
            uint slotResourceIndex = reader.ReadUInt32();

            resourceList.Add(new ResourceContractDescriptor(
                new ResourceOrdinal((uint)(ordinalBase + i)),
                resourceTypeMetadataName,
                access,
                sharing,
                aliasing,
                ownership,
                hasSlot,
                new SlotOrdinal(slot),
                slotResourceIndex));
        }

        return count;
    }

    private static int ReadPlanFields(ref Csp1Reader reader, List<ResourcePlanFieldDescriptor> planFieldList)
    {
        int count = ReadCount(
            ref reader,
            PipelineDescriptorLimits.MaximumPlanFieldsPerSlot,
            PipelineDescriptorLimits.ResourcePlanFieldDescriptorMinimumByteLength);

        for (int i = 0; i < count; i++)
        {
            if (reader.ReadUInt32() != (uint)i)
            {
                throw Invalid();
            }

            uint slotResourceIndex = reader.ReadUInt32();
            string memberMetadataName = ReadString(ref reader);
            string resourceTypeMetadataName = ReadString(ref reader);
            string planParameterName = ReadString(ref reader);
            ResourcePlanDimensionKind dimensionKind = (ResourcePlanDimensionKind)ReadEnumByte(ref reader, (byte)ResourcePlanDimensionKind.Height);

            planFieldList.Add(new ResourcePlanFieldDescriptor(
                (uint)i,
                slotResourceIndex,
                memberMetadataName,
                resourceTypeMetadataName,
                planParameterName,
                dimensionKind));
        }

        return count;
    }

    private static void ValidateSlotContract(ResourceOwnershipKind ownership, ResourcePlanKind planKind)
    {
        bool isValid = ownership switch
        {
            ResourceOwnershipKind.OwnedSlot => planKind is ResourcePlanKind.Buffer or ResourcePlanKind.Texture2D,
            ResourceOwnershipKind.OwnedGroupSlot => planKind is ResourcePlanKind.ResourceGroup,
            _ => false
        };

        if (!isValid)
        {
            throw Invalid();
        }
    }

    private static void ValidateResourceSlotBinding(ResourceContractDescriptor resource, OwnedSlotDescriptor[] slots, uint[] slotResourceCounts)
    {
        if (resource.Ownership is ResourceOwnershipKind.SharedTextureSlot)
        {
            throw Invalid();
        }

        bool expectsSlot = resource.Ownership is ResourceOwnershipKind.OwnedSlot or ResourceOwnershipKind.OwnedGroupSlot;

        if (resource.HasSlot != expectsSlot)
        {
            throw Invalid();
        }

        if (resource.Sharing is ComputeResourceSharing.External && resource.HasSlot)
        {
            throw Invalid();
        }

        if (!resource.HasSlot)
        {
            if (resource.Slot.Value != 0 || resource.SlotResourceIndex != 0)
            {
                throw Invalid();
            }

            return;
        }

        if (resource.Ownership is ResourceOwnershipKind.OwnedSlot or ResourceOwnershipKind.OwnedGroupSlot)
        {
            if (resource.Slot.Value >= (uint)slots.Length)
            {
                throw Invalid();
            }

            int slotIndex = (int)resource.Slot.Value;

            if (slots[slotIndex].Ownership != resource.Ownership)
            {
                throw Invalid();
            }

            if (resource.Ownership is ResourceOwnershipKind.OwnedSlot)
            {
                if (resource.SlotResourceIndex != 0)
                {
                    throw Invalid();
                }
            }
            else if (resource.SlotResourceIndex >= slotResourceCounts[slotIndex])
            {
                throw Invalid();
            }
        }
    }

    private static uint ValidateSlotPlanFields(ResourcePlanFieldDescriptor[] planFields, int start, int count, ResourcePlanKind planKind)
    {
        if (count == 0)
        {
            throw Invalid();
        }

        uint resourceCount = 0;

        for (int i = 0; i < count; i++)
        {
            uint slotResourceIndex = planFields[start + i].SlotResourceIndex;

            if (slotResourceIndex >= (uint)count)
            {
                throw Invalid();
            }

            resourceCount = Math.Max(resourceCount, slotResourceIndex + 1);

            for (int j = i + 1; j < count; j++)
            {
                if (planFields[start + i].PlanParameterName == planFields[start + j].PlanParameterName)
                {
                    throw Invalid();
                }
            }
        }

        if (planKind is ResourcePlanKind.Buffer or ResourcePlanKind.Texture2D && resourceCount != 1)
        {
            throw Invalid();
        }

        for (uint index = 0; index < resourceCount; index++)
        {
            ValidateSlotPlanResource(planFields, start, count, index, planKind);
        }

        return resourceCount;
    }

    private static void ValidateSlotPlanResource(ResourcePlanFieldDescriptor[] planFields, int start, int count, uint index, ResourcePlanKind planKind)
    {
        int dimensionMask = 0;
        string? memberMetadataName = null;
        string? resourceTypeMetadataName = null;

        for (int i = 0; i < count; i++)
        {
            ResourcePlanFieldDescriptor field = planFields[start + i];

            if (field.SlotResourceIndex != index)
            {
                continue;
            }

            int dimensionBit = 1 << (int)field.DimensionKind;

            if ((dimensionMask & dimensionBit) != 0)
            {
                throw Invalid();
            }

            if (memberMetadataName is not null &&
                (memberMetadataName != field.MemberMetadataName || resourceTypeMetadataName != field.ResourceTypeMetadataName))
            {
                throw Invalid();
            }

            dimensionMask |= dimensionBit;
            memberMetadataName = field.MemberMetadataName;
            resourceTypeMetadataName = field.ResourceTypeMetadataName;
        }

        if (dimensionMask is not (LengthDimensionMask or Texture2DDimensionMask))
        {
            throw Invalid();
        }

        if (planKind is ResourcePlanKind.Buffer && dimensionMask != LengthDimensionMask)
        {
            throw Invalid();
        }

        if (planKind is ResourcePlanKind.Texture2D && dimensionMask != Texture2DDimensionMask)
        {
            throw Invalid();
        }
    }

    private static void VerifyContractHash(ReadOnlySpan<byte> schemaHeader, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> expected)
    {
        using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        sha256.AppendData(schemaHeader);
        sha256.AppendData(payload);

        Span<byte> actual = stackalloc byte[32];

        _ = sha256.GetHashAndReset(actual);

        if (!actual.SequenceEqual(expected))
        {
            throw Invalid();
        }
    }

    private static ContractHash256 ReadContractHash256(ReadOnlySpan<byte> value)
    {
        return new ContractHash256(
            BinaryPrimitives.ReadUInt64LittleEndian(value.Slice(0, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(value.Slice(8, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(value.Slice(16, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(value.Slice(24, 8)));
    }

    private static string ReadString(ref Csp1Reader reader)
    {
        uint length = reader.ReadUInt32();

        if (length == NullStringMarker ||
            length > PipelineDescriptorLimits.MaximumStringUtf8ByteLength ||
            length > (uint)reader.Remaining)
        {
            throw Invalid();
        }

        string value;

        try
        {
            value = reader.ReadUtf8((int)length);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The canonical pipeline descriptor contains invalid UTF-8.", exception);
        }

        if (!value.IsNormalized(NormalizationForm.FormC))
        {
            throw Invalid();
        }

        return value;
    }

    private static int ReadCount(ref Csp1Reader reader, int maximumCount, int minimumElementByteLength)
    {
        uint count = reader.ReadUInt32();

        if (count > (uint)maximumCount || count > (uint)(reader.Remaining / minimumElementByteLength))
        {
            throw Invalid();
        }

        return (int)count;
    }

    private static int ReadInt32(ref Csp1Reader reader)
    {
        uint value = reader.ReadUInt32();

        if (value > int.MaxValue)
        {
            throw Invalid();
        }

        return (int)value;
    }

    private static bool ReadBoolean(ref Csp1Reader reader)
    {
        byte value = reader.ReadByte();

        return value switch
        {
            0 => false,
            1 => true,
            _ => throw Invalid()
        };
    }

    private static byte ReadEnumByte(ref Csp1Reader reader, byte maximumInclusive)
    {
        byte value = reader.ReadByte();

        if (value > maximumInclusive)
        {
            throw Invalid();
        }

        return value;
    }

    private static PipelineFlags ReadPipelineFlags(ref Csp1Reader reader)
    {
        uint value = reader.ReadUInt32();

        if ((value & ~((uint)PipelineFlags.InteropRoundTrip | (uint)PipelineFlags.UsesReadBack)) != 0)
        {
            throw Invalid();
        }

        return (PipelineFlags)value;
    }

    private static InvalidDataException Invalid()
    {
        return new InvalidDataException("The canonical pipeline descriptor is invalid.");
    }

    private readonly record struct PipelineIntermediate(
        string MethodMetadataName,
        string CanonicalSignature,
        PipelineFlags Flags,
        int MaximumTrackedResourceCount,
        int MaximumCommandListSegments,
        int ParameterStart,
        int ParameterCount,
        int InternalStart,
        int InternalCount);

    private readonly record struct SlotIntermediate(
        string MemberMetadataName,
        string ResourceTypeMetadataName,
        ResourceOwnershipKind Ownership,
        ResourcePlanKind PlanKind,
        ComputeResourceRecovery Recovery,
        int FieldStart,
        int FieldCount);
}
