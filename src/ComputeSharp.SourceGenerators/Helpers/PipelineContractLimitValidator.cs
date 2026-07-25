using System.Text;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The capability boundary checks a contract model must satisfy before it can be serialized.
/// </summary>
internal static class PipelineContractLimitValidator
{
    /// <summary>
    /// Checks whether a given pipeline host contract model is within the descriptor capability boundary.
    /// </summary>
    /// <param name="host">The pipeline host contract model to check.</param>
    /// <returns>Whether <paramref name="host"/> is within the descriptor capability boundary.</returns>
    public static bool IsWithinLimits(PipelineHostContractInfo host)
    {
        if (host.Pipelines.Length > PipelineDescriptorLimits.MaximumPipelineCount ||
            host.Slots.Length > PipelineDescriptorLimits.MaximumSlotCount ||
            !IsWithinStringLimit(host.HostTypeMetadataName))
        {
            return false;
        }

        foreach (PipelineContractInfo pipeline in host.Pipelines)
        {
            if (pipeline.MaximumTrackedResourceCount > PipelineDescriptorLimits.MaximumResourcesPerPipeline ||
                !IsWithinStringLimit(pipeline.MethodMetadataName) ||
                !IsWithinStringLimit(pipeline.CanonicalSignature))
            {
                return false;
            }

            foreach (ResourceContractInfo resource in pipeline.Parameters)
            {
                if (!IsWithinStringLimit(resource.ResourceTypeMetadataName))
                {
                    return false;
                }
            }

            foreach (ResourceContractInfo resource in pipeline.InternalResources)
            {
                if (!IsWithinStringLimit(resource.ResourceTypeMetadataName))
                {
                    return false;
                }
            }
        }

        foreach (OwnedSlotContractInfo slot in host.Slots)
        {
            if (slot.PlanFields.Length > PipelineDescriptorLimits.MaximumPlanFieldsPerSlot ||
                !IsWithinStringLimit(slot.MemberMetadataName) ||
                !IsWithinStringLimit(slot.ResourceTypeMetadataName))
            {
                return false;
            }

            foreach (ResourcePlanFieldContractInfo planField in slot.PlanFields)
            {
                if (!IsWithinStringLimit(planField.MemberMetadataName) ||
                    !IsWithinStringLimit(planField.ResourceTypeMetadataName) ||
                    !IsWithinStringLimit(planField.PlanParameterName))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a given interop resource set contract model is within the descriptor capability boundary.
    /// </summary>
    /// <param name="resourceSet">The interop resource set contract model to check.</param>
    /// <returns>Whether <paramref name="resourceSet"/> is within the descriptor capability boundary.</returns>
    public static bool IsWithinLimits(InteropResourceSetContractInfo resourceSet)
    {
        if (resourceSet.SharedTextures.Length > PipelineDescriptorLimits.MaximumSharedTextureCount ||
            !IsWithinStringLimit(resourceSet.ResourceSetTypeMetadataName))
        {
            return false;
        }

        foreach (SharedTextureContractInfo sharedTexture in resourceSet.SharedTextures)
        {
            if (!IsWithinStringLimit(sharedTexture.MemberMetadataName) ||
                !IsWithinStringLimit(sharedTexture.ResourceTypeMetadataName))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a given string is within the canonical string byte length limit.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>Whether <paramref name="value"/> is within the canonical string byte length limit.</returns>
    private static bool IsWithinStringLimit(string value)
    {
        return Encoding.UTF8.GetByteCount(value) <= PipelineDescriptorLimits.MaximumStringUtf8ByteLength;
    }
}
