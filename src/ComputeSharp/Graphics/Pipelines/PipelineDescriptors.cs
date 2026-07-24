using System;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly record struct PipelineSchemaVersion(ushort Major, ushort Minor, ushort Descriptor);

internal readonly struct ContractHash256(ulong littleEndianPart0, ulong littleEndianPart1, ulong littleEndianPart2, ulong littleEndianPart3)
{
    public ulong LittleEndianPart0 { get; } = littleEndianPart0;

    public ulong LittleEndianPart1 { get; } = littleEndianPart1;

    public ulong LittleEndianPart2 { get; } = littleEndianPart2;

    public ulong LittleEndianPart3 { get; } = littleEndianPart3;
}

internal readonly struct PipelineHostDescriptor(
    PipelineSchemaVersion schema,
    ContractHash256 contractHash,
    string hostTypeMetadataName,
    int maximumConcurrentInvocations,
    StaticStructuralRequirements structural,
    ReadOnlyMemory<PipelineDescriptor> pipelines,
    ReadOnlyMemory<OwnedSlotDescriptor> slots)
{
    public PipelineSchemaVersion Schema { get; } = schema;

    public ContractHash256 ContractHash { get; } = contractHash;

    public string HostTypeMetadataName { get; } = hostTypeMetadataName;

    public int MaximumConcurrentInvocations { get; } = maximumConcurrentInvocations;

    public StaticStructuralRequirements Structural { get; } = structural;

    public ReadOnlyMemory<PipelineDescriptor> Pipelines { get; } = pipelines;

    public ReadOnlyMemory<OwnedSlotDescriptor> Slots { get; } = slots;
}

internal readonly struct InteropResourceSetDescriptor(
    PipelineSchemaVersion schema,
    ContractHash256 contractHash,
    string resourceSetTypeMetadataName,
    ResourceSetStructuralRequirements structural,
    ReadOnlyMemory<SharedTextureContractDescriptor> sharedTextures)
{
    public PipelineSchemaVersion Schema { get; } = schema;

    public ContractHash256 ContractHash { get; } = contractHash;

    public string ResourceSetTypeMetadataName { get; } = resourceSetTypeMetadataName;

    public ResourceSetStructuralRequirements Structural { get; } = structural;

    public ReadOnlyMemory<SharedTextureContractDescriptor> SharedTextures { get; } = sharedTextures;
}

internal readonly struct SharedTextureContractDescriptor(
    SlotOrdinal ordinal,
    string memberMetadataName,
    string resourceTypeMetadataName,
    ComputeResourceResizePolicy resizePolicy,
    ComputeResourceAccess computeAccess,
    ExternalResourceAccess externalAccess,
    ExternalTextureUsage externalUsage,
    ComputeAlphaMode alphaMode,
    ComputeSharedTextureInitialOwner initialOwner,
    ComputeResourceRecovery recovery)
{
    public SlotOrdinal Ordinal { get; } = ordinal;

    public string MemberMetadataName { get; } = memberMetadataName;

    public string ResourceTypeMetadataName { get; } = resourceTypeMetadataName;

    public ComputeResourceResizePolicy ResizePolicy { get; } = resizePolicy;

    public ComputeResourceAccess ComputeAccess { get; } = computeAccess;

    public ExternalResourceAccess ExternalAccess { get; } = externalAccess;

    public ExternalTextureUsage ExternalUsage { get; } = externalUsage;

    public ComputeAlphaMode AlphaMode { get; } = alphaMode;

    public ComputeSharedTextureInitialOwner InitialOwner { get; } = initialOwner;

    public ComputeResourceRecovery Recovery { get; } = recovery;
}

internal readonly struct PipelineDescriptor(
    PipelineOrdinal ordinal,
    string methodMetadataName,
    string canonicalSignature,
    PipelineFlags flags,
    int maximumTrackedResourceCount,
    int maximumCommandListSegments,
    ReadOnlyMemory<ResourceContractDescriptor> parameters,
    ReadOnlyMemory<ResourceContractDescriptor> internalResources)
{
    public PipelineOrdinal Ordinal { get; } = ordinal;

    public string MethodMetadataName { get; } = methodMetadataName;

    public string CanonicalSignature { get; } = canonicalSignature;

    public PipelineFlags Flags { get; } = flags;

    public int MaximumTrackedResourceCount { get; } = maximumTrackedResourceCount;

    public int MaximumCommandListSegments { get; } = maximumCommandListSegments;

    public ReadOnlyMemory<ResourceContractDescriptor> Parameters { get; } = parameters;

    public ReadOnlyMemory<ResourceContractDescriptor> InternalResources { get; } = internalResources;
}

internal readonly struct ResourceContractDescriptor(
    ResourceOrdinal ordinal,
    string resourceTypeMetadataName,
    ComputeResourceAccess access,
    ComputeResourceSharing sharing,
    ComputeResourceAliasing aliasing,
    ResourceOwnershipKind ownership,
    bool hasSlot,
    SlotOrdinal slot,
    uint slotResourceIndex)
{
    public ResourceOrdinal Ordinal { get; } = ordinal;

    public string ResourceTypeMetadataName { get; } = resourceTypeMetadataName;

    public ComputeResourceAccess Access { get; } = access;

    public ComputeResourceSharing Sharing { get; } = sharing;

    public ComputeResourceAliasing Aliasing { get; } = aliasing;

    public ResourceOwnershipKind Ownership { get; } = ownership;

    public bool HasSlot { get; } = hasSlot;

    public SlotOrdinal Slot { get; } = slot;

    public uint SlotResourceIndex { get; } = slotResourceIndex;
}

internal readonly struct OwnedSlotDescriptor(
    SlotOrdinal ordinal,
    string memberMetadataName,
    string resourceTypeMetadataName,
    ResourceOwnershipKind ownership,
    ResourcePlanKind planKind,
    ComputeResourceRecovery recovery,
    ReadOnlyMemory<ResourcePlanFieldDescriptor> planFields)
{
    public SlotOrdinal Ordinal { get; } = ordinal;

    public string MemberMetadataName { get; } = memberMetadataName;

    public string ResourceTypeMetadataName { get; } = resourceTypeMetadataName;

    public ResourceOwnershipKind Ownership { get; } = ownership;

    public ResourcePlanKind PlanKind { get; } = planKind;

    public ComputeResourceRecovery Recovery { get; } = recovery;

    public ReadOnlyMemory<ResourcePlanFieldDescriptor> PlanFields { get; } = planFields;
}

internal readonly struct ResourcePlanFieldDescriptor(
    uint fieldOrdinal,
    uint slotResourceIndex,
    string memberMetadataName,
    string resourceTypeMetadataName,
    string planParameterName,
    ResourcePlanDimensionKind dimensionKind)
{
    public uint FieldOrdinal { get; } = fieldOrdinal;

    public uint SlotResourceIndex { get; } = slotResourceIndex;

    public string MemberMetadataName { get; } = memberMetadataName;

    public string ResourceTypeMetadataName { get; } = resourceTypeMetadataName;

    public string PlanParameterName { get; } = planParameterName;

    public ResourcePlanDimensionKind DimensionKind { get; } = dimensionKind;
}

internal readonly struct StaticStructuralRequirements(
    int maximumTrackedResourceCount,
    int maximumCommandListSegments,
    int ownedSlotCount)
{
    public int MaximumTrackedResourceCount { get; } = maximumTrackedResourceCount;

    public int MaximumCommandListSegments { get; } = maximumCommandListSegments;

    public int OwnedSlotCount { get; } = ownedSlotCount;
}

internal readonly struct ResourceSetStructuralRequirements(int sharedTextureSlotCount)
{
    public int SharedTextureSlotCount { get; } = sharedTextureSlotCount;
}
