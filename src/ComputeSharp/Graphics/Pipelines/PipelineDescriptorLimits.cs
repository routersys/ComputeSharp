namespace ComputeSharp.Graphics.Pipelines;

internal static class PipelineDescriptorLimits
{
    public const int MaximumDescriptorByteLength = 1_048_576;

    public const int MaximumPipelineCount = 256;

    public const int MaximumSlotCount = 256;

    public const int MaximumResourcesPerPipeline = 256;

    public const int MaximumPlanFieldsPerSlot = 256;

    public const int MaximumSharedTextureCount = 64;

    public const int MaximumStringUtf8ByteLength = 65_536;

    public const int PipelineDescriptorMinimumByteLength = 32;

    public const int ResourceContractDescriptorMinimumByteLength = 21;

    public const int OwnedSlotDescriptorMinimumByteLength = 19;

    public const int ResourcePlanFieldDescriptorMinimumByteLength = 21;

    public const int SharedTextureContractDescriptorMinimumByteLength = 19;
}
