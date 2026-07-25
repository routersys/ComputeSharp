using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A canonical contract model for a pipeline host type.
/// </summary>
/// <param name="HostTypeMetadataName">The fully qualified metadata name of the host type.</param>
/// <param name="MaximumConcurrentInvocations">The maximum number of concurrent invocations for the host.</param>
/// <param name="Structural">The static structural requirements for the host.</param>
/// <param name="Pipelines">The pipeline contracts, in canonical ordinal order.</param>
/// <param name="Slots">The owned slot contracts, in canonical ordinal order.</param>
internal sealed record PipelineHostContractInfo(
    string HostTypeMetadataName,
    int MaximumConcurrentInvocations,
    StructuralRequirementsInfo Structural,
    EquatableArray<PipelineContractInfo> Pipelines,
    EquatableArray<OwnedSlotContractInfo> Slots);

/// <summary>
/// A canonical contract model for an interop resource set type.
/// </summary>
/// <param name="ResourceSetTypeMetadataName">The fully qualified metadata name of the resource set type.</param>
/// <param name="SharedTextureSlotCount">The number of shared texture slots for the resource set.</param>
/// <param name="SharedTextures">The shared texture contracts, in canonical ordinal order.</param>
internal sealed record InteropResourceSetContractInfo(
    string ResourceSetTypeMetadataName,
    int SharedTextureSlotCount,
    EquatableArray<SharedTextureContractInfo> SharedTextures);

/// <summary>
/// A canonical contract model for a single pipeline method.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the pipeline within its host.</param>
/// <param name="MethodMetadataName">The metadata name of the pipeline method.</param>
/// <param name="CanonicalSignature">The canonical signature of the pipeline method.</param>
/// <param name="Flags">The flags describing the pipeline behavior.</param>
/// <param name="MaximumTrackedResourceCount">The maximum number of tracked resources for the pipeline.</param>
/// <param name="MaximumCommandListSegments">The maximum number of command list segments for the pipeline.</param>
/// <param name="Parameters">The resource contracts bound through parameters, in canonical ordinal order.</param>
/// <param name="InternalResources">The resource contracts owned by the host, in canonical ordinal order.</param>
internal sealed record PipelineContractInfo(
    uint Ordinal,
    string MethodMetadataName,
    string CanonicalSignature,
    PipelineFlags Flags,
    int MaximumTrackedResourceCount,
    int MaximumCommandListSegments,
    EquatableArray<ResourceContractInfo> Parameters,
    EquatableArray<ResourceContractInfo> InternalResources);

/// <summary>
/// A canonical contract model for a single resource bound by a pipeline.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the resource within its pipeline.</param>
/// <param name="ResourceTypeMetadataName">The fully qualified metadata name of the resource type.</param>
/// <param name="Access">The compute access for the resource.</param>
/// <param name="Sharing">The sharing mode for the resource.</param>
/// <param name="Aliasing">The aliasing mode for the resource.</param>
/// <param name="Ownership">The ownership kind for the resource.</param>
/// <param name="HasSlot">Whether the resource is bound to an owned slot.</param>
/// <param name="Slot">The referenced slot ordinal, or <c>0</c> when <paramref name="HasSlot"/> is <see langword="false"/>.</param>
/// <param name="SlotResourceIndex">The index within the referenced slot, or <c>0</c> when <paramref name="HasSlot"/> is <see langword="false"/>.</param>
internal sealed record ResourceContractInfo(
    uint Ordinal,
    string ResourceTypeMetadataName,
    ComputeResourceAccess Access,
    ComputeResourceSharing Sharing,
    ComputeResourceAliasing Aliasing,
    ResourceOwnershipKind Ownership,
    bool HasSlot,
    uint Slot,
    uint SlotResourceIndex);

/// <summary>
/// A canonical contract model for a host owned slot.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the slot within its host.</param>
/// <param name="MemberMetadataName">The metadata name of the owning member.</param>
/// <param name="ResourceTypeMetadataName">The fully qualified metadata name of the slot type.</param>
/// <param name="Ownership">The ownership kind for the slot.</param>
/// <param name="PlanKind">The plan kind for the slot.</param>
/// <param name="Recovery">The recovery class for the slot.</param>
/// <param name="PlanFields">The plan fields, in canonical field ordinal order.</param>
internal sealed record OwnedSlotContractInfo(
    uint Ordinal,
    string MemberMetadataName,
    string ResourceTypeMetadataName,
    ResourceOwnershipKind Ownership,
    ResourcePlanKind PlanKind,
    ComputeResourceRecovery Recovery,
    EquatableArray<ResourcePlanFieldContractInfo> PlanFields);

/// <summary>
/// A canonical contract model for a single scalar dimension of a resource plan.
/// </summary>
/// <param name="FieldOrdinal">The 0-based ordinal of the field within its slot.</param>
/// <param name="SlotResourceIndex">The index of the resource within its slot.</param>
/// <param name="MemberMetadataName">The metadata name of the owning member.</param>
/// <param name="ResourceTypeMetadataName">The fully qualified metadata name of the resource type.</param>
/// <param name="PlanParameterName">The name of the generated plan constructor parameter.</param>
/// <param name="DimensionKind">The dimension represented by the field.</param>
internal sealed record ResourcePlanFieldContractInfo(
    uint FieldOrdinal,
    uint SlotResourceIndex,
    string MemberMetadataName,
    string ResourceTypeMetadataName,
    string PlanParameterName,
    ResourcePlanDimensionKind DimensionKind);

/// <summary>
/// A canonical contract model for a shared texture owned by an interop resource set.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the shared texture slot within its resource set.</param>
/// <param name="MemberMetadataName">The metadata name of the owning member.</param>
/// <param name="ResourceTypeMetadataName">The fully qualified metadata name of the shared texture type.</param>
/// <param name="ResizePolicy">The resize policy for the shared texture.</param>
/// <param name="ComputeAccess">The compute access for the shared texture.</param>
/// <param name="ExternalAccess">The external access for the shared texture.</param>
/// <param name="ExternalUsage">The external usage for the shared texture.</param>
/// <param name="AlphaMode">The alpha mode for the shared texture.</param>
/// <param name="InitialOwner">The initial owner for the shared texture.</param>
/// <param name="Recovery">The recovery class for the shared texture.</param>
internal sealed record SharedTextureContractInfo(
    uint Ordinal,
    string MemberMetadataName,
    string ResourceTypeMetadataName,
    ComputeResourceResizePolicy ResizePolicy,
    ComputeResourceAccess ComputeAccess,
    ExternalResourceAccess ExternalAccess,
    ExternalTextureUsage ExternalUsage,
    ComputeAlphaMode AlphaMode,
    ComputeSharedTextureInitialOwner InitialOwner,
    ComputeResourceRecovery Recovery);

/// <summary>
/// A canonical contract model for the static structural requirements of a pipeline host.
/// </summary>
/// <param name="MaximumTrackedResourceCount">The maximum number of tracked resources across all pipelines.</param>
/// <param name="MaximumCommandListSegments">The maximum number of command list segments across all pipelines.</param>
/// <param name="OwnedSlotCount">The number of owned slots for the host.</param>
internal sealed record StructuralRequirementsInfo(
    int MaximumTrackedResourceCount,
    int MaximumCommandListSegments,
    int OwnedSlotCount);
