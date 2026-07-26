using System;

namespace ComputeSharp.Graphics.Pipelines;

internal enum ResourceContractValidationStatus : byte
{
    Valid = 0,
    ContractCountMismatch = 1,
    UnboundGeneration = 2,
    UndeclaredGeneration = 3,
    ObservedAccessExceedsDeclared = 4,
    AliasingNotAllowed = 5
}

internal static class ResourceContractValidator
{
    public static ResourceContractValidationStatus Validate(
        in PipelineDescriptor pipeline,
        ReadOnlySpan<ResourceGenerationId> boundGenerations,
        ReadOnlySpan<GraphicsResourceUsageEntry> usages)
    {
        ReadOnlySpan<ResourceContractDescriptor> parameters = pipeline.Parameters.Span;
        ReadOnlySpan<ResourceContractDescriptor> internalResources = pipeline.InternalResources.Span;

        if (boundGenerations.Length != checked(parameters.Length + internalResources.Length))
        {
            return ResourceContractValidationStatus.ContractCountMismatch;
        }

        for (int i = 0; i < boundGenerations.Length; i++)
        {
            if (boundGenerations[i].Value == 0)
            {
                return ResourceContractValidationStatus.UnboundGeneration;
            }
        }

        for (int i = 0; i < usages.Length; i++)
        {
            ResourceContractValidationStatus status = ValidateUsage(
                parameters,
                internalResources,
                boundGenerations,
                usages[i].Generation,
                usages[i].Access);

            if (status is not ResourceContractValidationStatus.Valid)
            {
                return status;
            }
        }

        return ResourceContractValidationStatus.Valid;
    }

    private static ResourceContractValidationStatus ValidateUsage(
        ReadOnlySpan<ResourceContractDescriptor> parameters,
        ReadOnlySpan<ResourceContractDescriptor> internalResources,
        ReadOnlySpan<ResourceGenerationId> boundGenerations,
        ResourceGenerationId generation,
        ComputeResourceAccess observedAccess)
    {
        int declaringCount = 0;
        bool isAliasingAllowed = true;
        ComputeResourceAliasing previousAliasing = default;

        for (int i = 0; i < boundGenerations.Length; i++)
        {
            if (boundGenerations[i] != generation)
            {
                continue;
            }

            ref readonly ResourceContractDescriptor contract = ref i < parameters.Length
                ? ref parameters[i]
                : ref internalResources[i - parameters.Length];

            if (!ResourceUsageTracker.IsWithinDeclared(observedAccess, contract.Access))
            {
                return ResourceContractValidationStatus.ObservedAccessExceedsDeclared;
            }

            if (declaringCount > 0 && !ResourceUsageTracker.IsAliasingAllowed(previousAliasing, contract.Aliasing))
            {
                isAliasingAllowed = false;
            }

            previousAliasing = contract.Aliasing;
            declaringCount++;
        }

        if (declaringCount == 0)
        {
            return ResourceContractValidationStatus.UndeclaredGeneration;
        }

        if (declaringCount > 1 && !isAliasingAllowed)
        {
            return ResourceContractValidationStatus.AliasingNotAllowed;
        }

        return ResourceContractValidationStatus.Valid;
    }
}
