using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A canonical key identifying the owned slot a resource contract is bound to.
/// </summary>
/// <param name="HostMemberMetadataName">The metadata name of the host member declaring the slot.</param>
internal readonly record struct SlotContractKey(string HostMemberMetadataName);

/// <summary>
/// An internal resource contract that still carries the canonical member keys needed to order it.
/// </summary>
/// <param name="HostMemberMetadataName">The metadata name of the host member declaring the resource.</param>
/// <param name="GroupMemberMetadataName">The metadata name of the resource group member, if the resource comes from a group.</param>
/// <param name="ResourceTypeMetadataName">The canonical metadata name of the graphics resource type.</param>
/// <param name="Access">The compute access for the resource.</param>
/// <param name="Sharing">The sharing mode for the resource.</param>
/// <param name="Aliasing">The aliasing mode for the resource.</param>
/// <param name="Ownership">The ownership kind for the resource.</param>
/// <param name="SlotResourceIndex">The index within the referenced slot.</param>
/// <param name="SlotKey">The key of the referenced slot, or <see langword="null"/> for a borrowed resource.</param>
internal sealed record UnorderedInternalResourceContract(
    string HostMemberMetadataName,
    string? GroupMemberMetadataName,
    string ResourceTypeMetadataName,
    ComputeResourceAccess Access,
    ComputeResourceSharing Sharing,
    ComputeResourceAliasing Aliasing,
    ResourceOwnershipKind Ownership,
    uint SlotResourceIndex,
    SlotContractKey? SlotKey);
