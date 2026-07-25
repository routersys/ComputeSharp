using System.Collections.Generic;
using System.Collections.Immutable;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The collection rules for the graphics resources declared by a compute pipeline host.
/// </summary>
internal static class HostResourceCollector
{
    /// <summary>
    /// Tries to collect the owned slots and internal resource contracts declared by a given host.
    /// </summary>
    /// <param name="hostSymbol">The host type to collect the resources of.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="slots">The resulting owned slots, in canonical order.</param>
    /// <param name="resources">The resulting internal resource contracts, in canonical order.</param>
    /// <returns>Whether every declaration of <paramref name="hostSymbol"/> could be collected.</returns>
    public static bool TryCollect(
        INamedTypeSymbol hostSymbol,
        PipelineWellKnownSymbols symbols,
        out EquatableArray<OwnedSlotContractInfo> slots,
        out EquatableArray<UnorderedInternalResourceContract> resources)
    {
        using ImmutableArrayBuilder<OwnedSlotContractInfo> slotBuilder = new();
        using ImmutableArrayBuilder<UnorderedInternalResourceContract> resourceBuilder = new();

        HashSet<string> canonicalNames = [];

        foreach (ISymbol memberSymbol in hostSymbol.GetMembers())
        {
            if (!memberSymbol.TryGetAttributeWithType(symbols.PipelineResourceAttribute, out AttributeData? attribute))
            {
                continue;
            }

            if (memberSymbol is not IFieldSymbol fieldSymbol ||
                !GeneratedIdentifier.TryCreateCanonicalName(fieldSymbol.MetadataName, out string canonicalName) ||
                !canonicalNames.Add(canonicalName) ||
                !TryCollectMember(fieldSymbol, attribute, symbols, in slotBuilder, in resourceBuilder))
            {
                slots = default;
                resources = default;

                return false;
            }
        }

        slots = PipelineCanonicalOrdering.OrderSlots(slotBuilder.ToImmutable());
        resources = OrderResources(resourceBuilder.ToImmutable());

        return true;
    }

    /// <summary>
    /// Tries to collect a single annotated host member.
    /// </summary>
    /// <param name="fieldSymbol">The annotated host field.</param>
    /// <param name="attribute">The <c>[ComputePipelineResource]</c> data of the field.</param>
    /// <param name="symbols">The well known symbols to resolve the declaration with.</param>
    /// <param name="slotBuilder">The target owned slot builder.</param>
    /// <param name="resourceBuilder">The target internal resource builder.</param>
    /// <returns>Whether the member could be collected.</returns>
    private static bool TryCollectMember(
        IFieldSymbol fieldSymbol,
        AttributeData attribute,
        PipelineWellKnownSymbols symbols,
        ref readonly ImmutableArrayBuilder<OwnedSlotContractInfo> slotBuilder,
        ref readonly ImmutableArrayBuilder<UnorderedInternalResourceContract> resourceBuilder)
    {
        if (!fieldSymbol.IsReadOnly ||
            !TryGetAccess(attribute, out ComputeResourceAccess access, out bool hasRecovery, out ComputeResourceRecovery recovery))
        {
            return false;
        }

        if (fieldSymbol.Type is INamedTypeSymbol { IsGenericType: true } slotTypeSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, symbols.ResourceSlot))
            {
                return hasRecovery &&
                    HasObjectCreationInitializer(fieldSymbol) &&
                    TryCollectOwnedSlot(fieldSymbol, slotTypeSymbol.TypeArguments[0], access, recovery, in slotBuilder, in resourceBuilder);
            }

            if (SymbolEqualityComparer.Default.Equals(slotTypeSymbol.OriginalDefinition, symbols.ResourceGroupSlot))
            {
                return hasRecovery &&
                    HasObjectCreationInitializer(fieldSymbol) &&
                    TryCollectOwnedGroupSlot(fieldSymbol, slotTypeSymbol.TypeArguments[0], access, recovery, symbols, in slotBuilder, in resourceBuilder);
            }
        }

        if (hasRecovery || !fieldSymbol.Type.HasInterfaceWithType(symbols.GraphicsResourceInterface))
        {
            return false;
        }

        resourceBuilder.Add(new UnorderedInternalResourceContract(
            fieldSymbol.MetadataName,
            null,
            CanonicalTypeNameBuilder.GetCanonicalTypeName(fieldSymbol.Type),
            access,
            ComputeResourceSharing.Internal,
            ComputeResourceAliasing.Disallow,
            ResourceOwnershipKind.Borrowed,
            0,
            null));

        return true;
    }

    /// <summary>
    /// Tries to collect a single owned resource slot.
    /// </summary>
    /// <param name="fieldSymbol">The annotated host field.</param>
    /// <param name="resourceTypeSymbol">The owned resource type.</param>
    /// <param name="access">The declared compute access.</param>
    /// <param name="recovery">The declared recovery class.</param>
    /// <param name="slotBuilder">The target owned slot builder.</param>
    /// <param name="resourceBuilder">The target internal resource builder.</param>
    /// <returns>Whether the owned slot could be collected.</returns>
    private static bool TryCollectOwnedSlot(
        IFieldSymbol fieldSymbol,
        ITypeSymbol resourceTypeSymbol,
        ComputeResourceAccess access,
        ComputeResourceRecovery recovery,
        ref readonly ImmutableArrayBuilder<OwnedSlotContractInfo> slotBuilder,
        ref readonly ImmutableArrayBuilder<UnorderedInternalResourceContract> resourceBuilder)
    {
        using ImmutableArrayBuilder<ResourcePlanFieldContractInfo> planFieldBuilder = new();

        if (!ResourcePlanGrammar.TryGetPlanKind(resourceTypeSymbol, out ResourcePlanKind planKind) ||
            !ResourcePlanGrammar.TryAppendPlanFields(resourceTypeSymbol, fieldSymbol.MetadataName, 0, in planFieldBuilder))
        {
            return false;
        }

        string resourceTypeMetadataName = CanonicalTypeNameBuilder.GetCanonicalTypeName(resourceTypeSymbol);

        slotBuilder.Add(new OwnedSlotContractInfo(
            uint.MaxValue,
            fieldSymbol.MetadataName,
            resourceTypeMetadataName,
            ResourceOwnershipKind.OwnedSlot,
            planKind,
            recovery,
            PipelineCanonicalOrdering.OrderPlanFields(planFieldBuilder.ToImmutable())));

        resourceBuilder.Add(new UnorderedInternalResourceContract(
            fieldSymbol.MetadataName,
            null,
            resourceTypeMetadataName,
            access,
            ComputeResourceSharing.Internal,
            ComputeResourceAliasing.Disallow,
            ResourceOwnershipKind.OwnedSlot,
            0,
            new SlotContractKey(fieldSymbol.MetadataName)));

        return true;
    }

    /// <summary>
    /// Tries to collect a single owned resource group slot.
    /// </summary>
    /// <param name="fieldSymbol">The annotated host field.</param>
    /// <param name="groupTypeSymbol">The owned resource group type.</param>
    /// <param name="slotAccess">The declared aggregate compute access of the slot.</param>
    /// <param name="recovery">The declared recovery class.</param>
    /// <param name="symbols">The well known symbols to resolve the declaration with.</param>
    /// <param name="slotBuilder">The target owned slot builder.</param>
    /// <param name="resourceBuilder">The target internal resource builder.</param>
    /// <returns>Whether the owned resource group slot could be collected.</returns>
    private static bool TryCollectOwnedGroupSlot(
        IFieldSymbol fieldSymbol,
        ITypeSymbol groupTypeSymbol,
        ComputeResourceAccess slotAccess,
        ComputeResourceRecovery recovery,
        PipelineWellKnownSymbols symbols,
        ref readonly ImmutableArrayBuilder<OwnedSlotContractInfo> slotBuilder,
        ref readonly ImmutableArrayBuilder<UnorderedInternalResourceContract> resourceBuilder)
    {
        if (groupTypeSymbol is not INamedTypeSymbol { IsGenericType: false } groupSymbol ||
            !groupSymbol.HasAttributeWithType(symbols.ResourceGroupAttribute) ||
            !TryCollectGroupMembers(groupSymbol, symbols, out IPropertySymbol[]? groupMembers))
        {
            return false;
        }

        using ImmutableArrayBuilder<ResourcePlanFieldContractInfo> planFieldBuilder = new();
        using ImmutableArrayBuilder<UnorderedInternalResourceContract> memberBuilder = new();

        HashSet<string> canonicalNames = [];

        for (int i = 0; i < groupMembers.Length; i++)
        {
            IPropertySymbol memberSymbol = groupMembers[i];

            if (!memberSymbol.TryGetAttributeWithType(symbols.PipelineResourceAttribute, out AttributeData? memberAttribute) ||
                !TryGetAccess(memberAttribute, out ComputeResourceAccess memberAccess, out bool memberHasRecovery, out _) ||
                memberHasRecovery ||
                !IsAccessWithin(memberAccess, slotAccess) ||
                !GeneratedIdentifier.TryCreateCanonicalName(memberSymbol.MetadataName, out string canonicalName) ||
                !canonicalNames.Add(canonicalName) ||
                !ResourcePlanGrammar.TryAppendPlanFields(memberSymbol.Type, memberSymbol.MetadataName, (uint)i, in planFieldBuilder))
            {
                return false;
            }

            memberBuilder.Add(new UnorderedInternalResourceContract(
                fieldSymbol.MetadataName,
                memberSymbol.MetadataName,
                CanonicalTypeNameBuilder.GetCanonicalTypeName(memberSymbol.Type),
                memberAccess,
                ComputeResourceSharing.Internal,
                ComputeResourceAliasing.Disallow,
                ResourceOwnershipKind.OwnedGroupSlot,
                (uint)i,
                new SlotContractKey(fieldSymbol.MetadataName)));
        }

        slotBuilder.Add(new OwnedSlotContractInfo(
            uint.MaxValue,
            fieldSymbol.MetadataName,
            CanonicalTypeNameBuilder.GetCanonicalTypeName(groupSymbol),
            ResourceOwnershipKind.OwnedGroupSlot,
            ResourcePlanKind.ResourceGroup,
            recovery,
            PipelineCanonicalOrdering.OrderPlanFields(planFieldBuilder.ToImmutable())));

        resourceBuilder.AddRange(memberBuilder.WrittenSpan);

        return true;
    }

    /// <summary>
    /// Tries to collect every annotated member of a given resource group, in canonical order.
    /// </summary>
    /// <param name="groupSymbol">The resource group type.</param>
    /// <param name="symbols">The well known symbols to resolve the declarations with.</param>
    /// <param name="groupMembers">The resulting annotated members, in canonical order.</param>
    /// <returns>Whether every annotated member of <paramref name="groupSymbol"/> is a valid group member.</returns>
    private static bool TryCollectGroupMembers(
        INamedTypeSymbol groupSymbol,
        PipelineWellKnownSymbols symbols,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPropertySymbol[]? groupMembers)
    {
        using ImmutableArrayBuilder<IPropertySymbol> builder = new();

        foreach (ISymbol memberSymbol in groupSymbol.GetMembers())
        {
            if (!memberSymbol.HasAttributeWithType(symbols.PipelineResourceAttribute))
            {
                continue;
            }

            if (memberSymbol is not IPropertySymbol { SetMethod: null, GetMethod: not null, IsIndexer: false, IsStatic: false } propertySymbol)
            {
                groupMembers = null;

                return false;
            }

            builder.Add(propertySymbol);
        }

        if (builder.Count == 0)
        {
            groupMembers = null;

            return false;
        }

        IPropertySymbol[] members = [.. builder.ToImmutable()];

        System.Array.Sort(members, static (left, right) => string.CompareOrdinal(left.MetadataName, right.MetadataName));

        groupMembers = members;

        return true;
    }

    /// <summary>
    /// Orders a set of internal resource contracts by their canonical member keys.
    /// </summary>
    /// <param name="resources">The internal resource contracts to order.</param>
    /// <returns>The resulting ordered internal resource contracts.</returns>
    private static EquatableArray<UnorderedInternalResourceContract> OrderResources(ImmutableArray<UnorderedInternalResourceContract> resources)
    {
        UnorderedInternalResourceContract[] items = [.. resources];

        System.Array.Sort(items, static (left, right) =>
        {
            int comparison = string.CompareOrdinal(left.HostMemberMetadataName, right.HostMemberMetadataName);

            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.GroupMemberMetadataName, right.GroupMemberMetadataName);
        });

        return ImmutableArray.Create(items);
    }

    /// <summary>
    /// Tries to get the declared contract values of a <c>[ComputePipelineResource]</c> attribute.
    /// </summary>
    /// <param name="attribute">The attribute data to read.</param>
    /// <param name="access">The declared compute access.</param>
    /// <param name="hasRecovery">Whether a recovery class was declared.</param>
    /// <param name="recovery">The declared recovery class.</param>
    /// <returns>Whether the attribute declares a supported contract.</returns>
    private static bool TryGetAccess(
        AttributeData attribute,
        out ComputeResourceAccess access,
        out bool hasRecovery,
        out ComputeResourceRecovery recovery)
    {
        access = default;
        hasRecovery = false;
        recovery = default;

        switch (attribute.ConstructorArguments)
        {
            case [{ Value: byte accessValue }]:
                access = (ComputeResourceAccess)accessValue;

                return IsKnownAccess(access);
            case [{ Value: byte accessValueWithRecovery }, { Value: byte recoveryValue }]:
                access = (ComputeResourceAccess)accessValueWithRecovery;
                hasRecovery = true;
                recovery = (ComputeResourceRecovery)recoveryValue;

                return IsKnownAccess(access) && IsKnownRecovery(recovery);
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a given field is initialized in its declaration with an object creation expression.
    /// </summary>
    /// <param name="fieldSymbol">The field to check.</param>
    /// <returns>Whether <paramref name="fieldSymbol"/> is initialized in its declaration with an object creation expression.</returns>
    private static bool HasObjectCreationInitializer(IFieldSymbol fieldSymbol)
    {
        foreach (SyntaxReference syntaxReference in fieldSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: BaseObjectCreationExpressionSyntax })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a given compute access is a known value.
    /// </summary>
    /// <param name="access">The compute access to check.</param>
    /// <returns>Whether <paramref name="access"/> is a known value.</returns>
    private static bool IsKnownAccess(ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.Read or ComputeResourceAccess.Write or ComputeResourceAccess.ReadWrite;
    }

    /// <summary>
    /// Checks whether a given recovery class is a known value.
    /// </summary>
    /// <param name="recovery">The recovery class to check.</param>
    /// <returns>Whether <paramref name="recovery"/> is a known value.</returns>
    private static bool IsKnownRecovery(ComputeResourceRecovery recovery)
    {
        return recovery is
            ComputeResourceRecovery.Discardable or
            ComputeResourceRecovery.RecreateFromHost or
            ComputeResourceRecovery.Recompute or
            ComputeResourceRecovery.CapacityOnly;
    }

    /// <summary>
    /// Checks whether a member compute access is within an aggregate slot compute access.
    /// </summary>
    /// <param name="memberAccess">The member compute access.</param>
    /// <param name="slotAccess">The aggregate slot compute access.</param>
    /// <returns>Whether <paramref name="memberAccess"/> is within <paramref name="slotAccess"/>.</returns>
    private static bool IsAccessWithin(ComputeResourceAccess memberAccess, ComputeResourceAccess slotAccess)
    {
        return slotAccess is ComputeResourceAccess.ReadWrite || memberAccess == slotAccess;
    }
}
