using ComputeSharp.SourceGeneration.Helpers;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A canonical contract model for a resource group type.
/// </summary>
/// <param name="GroupTypeMetadataName">The fully qualified metadata name of the resource group type.</param>
/// <param name="Members">The group member contracts, in canonical member order.</param>
/// <param name="PlanFields">The plan fields of the group, in canonical field ordinal order.</param>
internal sealed record ResourceGroupContractInfo(
    string GroupTypeMetadataName,
    EquatableArray<ResourceGroupMemberContractInfo> Members,
    EquatableArray<ResourcePlanFieldContractInfo> PlanFields);

/// <summary>
/// A canonical contract model for a single member of a resource group.
/// </summary>
/// <param name="SlotResourceIndex">The index of the member within its group.</param>
/// <param name="MemberMetadataName">The metadata name of the member.</param>
/// <param name="ResourceTypeMetadataName">The fully qualified metadata name of the member resource type.</param>
/// <param name="Access">The compute access declared by the member.</param>
internal sealed record ResourceGroupMemberContractInfo(
    uint SlotResourceIndex,
    string MemberMetadataName,
    string ResourceTypeMetadataName,
    ComputeResourceAccess Access);
