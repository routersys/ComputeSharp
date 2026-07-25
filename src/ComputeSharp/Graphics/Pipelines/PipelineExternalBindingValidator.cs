using System;
using System.IO;

namespace ComputeSharp.Graphics.Pipelines;

internal static class PipelineExternalBindingValidator
{
    public static void Validate(in PipelineHostDescriptor host, in InteropResourceSetDescriptor resourceSet)
    {
        if (host.Schema != resourceSet.Schema)
        {
            throw Invalid();
        }

        ReadOnlySpan<PipelineDescriptor> pipelines = host.Pipelines.Span;
        ReadOnlySpan<SharedTextureContractDescriptor> sharedTextures = resourceSet.SharedTextures.Span;

        for (int i = 0; i < pipelines.Length; i++)
        {
            ValidateResources(pipelines[i].Parameters.Span, sharedTextures);
            ValidateResources(pipelines[i].InternalResources.Span, sharedTextures);
        }
    }

    private static void ValidateResources(ReadOnlySpan<ResourceContractDescriptor> resources, ReadOnlySpan<SharedTextureContractDescriptor> sharedTextures)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            ResourceContractDescriptor resource = resources[i];

            if (resource.Ownership is not ResourceOwnershipKind.SharedTextureSlot)
            {
                continue;
            }

            if (resource.Slot.Value >= (uint)sharedTextures.Length ||
                resource.SlotResourceIndex != 0 ||
                resource.Sharing is not ComputeResourceSharing.External)
            {
                throw Invalid();
            }

            SharedTextureContractDescriptor sharedTexture = sharedTextures[(int)resource.Slot.Value];

            if (resource.ResourceTypeMetadataName != sharedTexture.ResourceTypeMetadataName ||
                resource.Access != sharedTexture.ComputeAccess)
            {
                throw Invalid();
            }
        }
    }

    private static InvalidDataException Invalid()
    {
        return new InvalidDataException("The canonical pipeline descriptor declares an invalid external binding.");
    }
}
