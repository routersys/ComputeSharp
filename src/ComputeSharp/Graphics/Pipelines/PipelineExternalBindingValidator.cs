using System.IO;

namespace ComputeSharp.Graphics.Pipelines;

internal static class PipelineExternalBindingValidator
{
    public static void Validate(in ResourceContractDescriptor parameter, in SharedTextureContractDescriptor sharedTexture)
    {
        if (parameter.Sharing is not ComputeResourceSharing.External ||
            parameter.Ownership is not ResourceOwnershipKind.Borrowed ||
            parameter.HasSlot ||
            parameter.Slot.Value != 0 ||
            parameter.SlotResourceIndex != 0)
        {
            throw Invalid();
        }

        if (parameter.ResourceTypeMetadataName != sharedTexture.ResourceTypeMetadataName ||
            parameter.Access != sharedTexture.ComputeAccess)
        {
            throw Invalid();
        }
    }

    private static InvalidDataException Invalid()
    {
        return new InvalidDataException("The canonical pipeline descriptor declares an invalid external binding.");
    }
}
