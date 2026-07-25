using System.Security.Cryptography;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The writer of the canonical binary descriptors of a compute pipeline host or interop resource set.
/// </summary>
internal static class PipelineDescriptorWriter
{
    /// <summary>
    /// The size in bytes of the descriptor wire header.
    /// </summary>
    private const int HeaderSize = 48;

    /// <summary>
    /// Writes the canonical binary descriptor of a given pipeline host contract model.
    /// </summary>
    /// <param name="host">The pipeline host contract model to write.</param>
    /// <returns>The canonical binary descriptor of <paramref name="host"/>.</returns>
    public static byte[] Write(PipelineHostContractInfo host)
    {
        Csp1Writer writer = new();

        writer.WriteEnumByte(DescriptorKind.PipelineHost);
        writer.WriteString(host.HostTypeMetadataName);
        writer.WriteInt32(host.MaximumConcurrentInvocations);
        writer.WriteInt32(host.Structural.MaximumTrackedResourceCount);
        writer.WriteInt32(host.Structural.MaximumCommandListSegments);
        writer.WriteInt32(host.Structural.OwnedSlotCount);
        writer.WriteUInt32((uint)host.Pipelines.Length);

        foreach (PipelineContractInfo pipeline in host.Pipelines)
        {
            WritePipeline(writer, pipeline);
        }

        writer.WriteUInt32((uint)host.Slots.Length);

        foreach (OwnedSlotContractInfo slot in host.Slots)
        {
            WriteSlot(writer, slot);
        }

        return CreateDescriptor(writer.ToArray());
    }

    /// <summary>
    /// Writes the canonical binary descriptor of a given interop resource set contract model.
    /// </summary>
    /// <param name="resourceSet">The interop resource set contract model to write.</param>
    /// <returns>The canonical binary descriptor of <paramref name="resourceSet"/>.</returns>
    public static byte[] Write(InteropResourceSetContractInfo resourceSet)
    {
        Csp1Writer writer = new();

        writer.WriteEnumByte(DescriptorKind.InteropResourceSet);
        writer.WriteString(resourceSet.ResourceSetTypeMetadataName);
        writer.WriteInt32(resourceSet.SharedTextureSlotCount);
        writer.WriteUInt32((uint)resourceSet.SharedTextures.Length);

        foreach (SharedTextureContractInfo sharedTexture in resourceSet.SharedTextures)
        {
            writer.WriteUInt32(sharedTexture.Ordinal);
            writer.WriteString(sharedTexture.MemberMetadataName);
            writer.WriteString(sharedTexture.ResourceTypeMetadataName);
            writer.WriteEnumByte(sharedTexture.ResizePolicy);
            writer.WriteEnumByte(sharedTexture.ComputeAccess);
            writer.WriteEnumByte(sharedTexture.ExternalAccess);
            writer.WriteEnumByte(sharedTexture.ExternalUsage);
            writer.WriteEnumByte(sharedTexture.AlphaMode);
            writer.WriteEnumByte(sharedTexture.InitialOwner);
            writer.WriteEnumByte(sharedTexture.Recovery);
        }

        return CreateDescriptor(writer.ToArray());
    }

    /// <summary>
    /// Writes a single pipeline contract.
    /// </summary>
    /// <param name="writer">The target writer.</param>
    /// <param name="pipeline">The pipeline contract to write.</param>
    private static void WritePipeline(Csp1Writer writer, PipelineContractInfo pipeline)
    {
        writer.WriteUInt32(pipeline.Ordinal);
        writer.WriteString(pipeline.MethodMetadataName);
        writer.WriteString(pipeline.CanonicalSignature);
        writer.WriteUInt32((uint)pipeline.Flags);
        writer.WriteInt32(pipeline.MaximumTrackedResourceCount);
        writer.WriteInt32(pipeline.MaximumCommandListSegments);
        writer.WriteUInt32((uint)pipeline.Parameters.Length);

        foreach (ResourceContractInfo resource in pipeline.Parameters)
        {
            WriteResource(writer, resource);
        }

        writer.WriteUInt32((uint)pipeline.InternalResources.Length);

        foreach (ResourceContractInfo resource in pipeline.InternalResources)
        {
            WriteResource(writer, resource);
        }
    }

    /// <summary>
    /// Writes a single resource contract.
    /// </summary>
    /// <param name="writer">The target writer.</param>
    /// <param name="resource">The resource contract to write.</param>
    private static void WriteResource(Csp1Writer writer, ResourceContractInfo resource)
    {
        writer.WriteUInt32(resource.Ordinal);
        writer.WriteString(resource.ResourceTypeMetadataName);
        writer.WriteEnumByte(resource.Access);
        writer.WriteEnumByte(resource.Sharing);
        writer.WriteEnumByte(resource.Aliasing);
        writer.WriteEnumByte(resource.Ownership);
        writer.WriteBoolean(resource.HasSlot);
        writer.WriteUInt32(resource.Slot);
        writer.WriteUInt32(resource.SlotResourceIndex);
    }

    /// <summary>
    /// Writes a single owned slot contract.
    /// </summary>
    /// <param name="writer">The target writer.</param>
    /// <param name="slot">The owned slot contract to write.</param>
    private static void WriteSlot(Csp1Writer writer, OwnedSlotContractInfo slot)
    {
        writer.WriteUInt32(slot.Ordinal);
        writer.WriteString(slot.MemberMetadataName);
        writer.WriteString(slot.ResourceTypeMetadataName);
        writer.WriteEnumByte(slot.Ownership);
        writer.WriteEnumByte(slot.PlanKind);
        writer.WriteEnumByte(slot.Recovery);
        writer.WriteUInt32((uint)slot.PlanFields.Length);

        foreach (ResourcePlanFieldContractInfo planField in slot.PlanFields)
        {
            writer.WriteUInt32(planField.FieldOrdinal);
            writer.WriteUInt32(planField.SlotResourceIndex);
            writer.WriteString(planField.MemberMetadataName);
            writer.WriteString(planField.ResourceTypeMetadataName);
            writer.WriteString(planField.PlanParameterName);
            writer.WriteEnumByte(planField.DimensionKind);
        }
    }

    /// <summary>
    /// Creates the complete descriptor of a given payload, with its wire header and contract hash.
    /// </summary>
    /// <param name="payload">The payload to create the descriptor of.</param>
    /// <returns>The complete descriptor of <paramref name="payload"/>.</returns>
    private static byte[] CreateDescriptor(byte[] payload)
    {
        Csp1Writer schemaWriter = new();

        schemaWriter.WriteByte((byte)'C');
        schemaWriter.WriteByte((byte)'S');
        schemaWriter.WriteByte((byte)'P');
        schemaWriter.WriteByte((byte)'1');
        schemaWriter.WriteUInt16(PipelineSchema.Major);
        schemaWriter.WriteUInt16(PipelineSchema.Minor);
        schemaWriter.WriteUInt16(PipelineSchema.DescriptorFormat);

        byte[] hashedPrefix = schemaWriter.ToArray();
        byte[] hashInput = new byte[hashedPrefix.Length + payload.Length];

        hashedPrefix.CopyTo(hashInput, 0);
        payload.CopyTo(hashInput, hashedPrefix.Length);

        byte[] contractHash;

        using (SHA256 sha256 = SHA256.Create())
        {
            contractHash = sha256.ComputeHash(hashInput);
        }

        Csp1Writer headerWriter = new();

        headerWriter.WriteByte((byte)'C');
        headerWriter.WriteByte((byte)'S');
        headerWriter.WriteByte((byte)'P');
        headerWriter.WriteByte((byte)'1');
        headerWriter.WriteUInt16(PipelineSchema.Major);
        headerWriter.WriteUInt16(PipelineSchema.Minor);
        headerWriter.WriteUInt16(PipelineSchema.DescriptorFormat);
        headerWriter.WriteUInt16(0);
        headerWriter.WriteUInt32((uint)payload.Length);

        byte[] header = headerWriter.ToArray();
        byte[] descriptor = new byte[HeaderSize + payload.Length];

        header.CopyTo(descriptor, 0);
        contractHash.CopyTo(descriptor, header.Length);
        payload.CopyTo(descriptor, HeaderSize);

        return descriptor;
    }
}
