using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal static class ResourceUsageTracker
{
    private const int ReadBit = 1;

    private const int WriteBit = 2;

    public static ComputeResourceAccess Union(ComputeResourceAccess left, ComputeResourceAccess right)
    {
        return FromBits(ToBits(left) | ToBits(right));
    }

    public static bool IsWithinDeclared(ComputeResourceAccess observed, ComputeResourceAccess declared)
    {
        return (ToBits(observed) & ~ToBits(declared)) == 0;
    }

    public static bool IsAliasingAllowed(ComputeResourceAliasing left, ComputeResourceAliasing right)
    {
        return left is ComputeResourceAliasing.Allow && right is ComputeResourceAliasing.Allow;
    }

    public static bool TryAddUsage(
        Span<GraphicsResourceUsageEntry> storage,
        ref UsageSetPoolEntry usageSet,
        in ResourceGenerationSetHandle set,
        uint resourceIndex,
        ResourceGenerationId generation,
        ComputeResourceAccess observedAccess,
        TrackedResourceState firstState,
        TrackedResourceState finalState,
        out int entryIndex,
        out bool isAliased)
    {
        default(ArgumentException).ThrowIf(generation.Value == 0, nameof(generation));
        default(ArgumentException).ThrowIf(set.IsEmpty, nameof(set));

        Span<GraphicsResourceUsageEntry> entries = GetEntries(storage, in usageSet);

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Generation != generation)
            {
                continue;
            }

            default(InvalidOperationException).ThrowIf(
                entries[i].Set.SetId != set.SetId || entries[i].ResourceIndex != resourceIndex,
                "The resource generation is already tracked under a different generation set slot.");

            entries[i].Access = Union(entries[i].Access, observedAccess);
            entries[i].FinalState = finalState;

            entryIndex = i;
            isAliased = true;

            return true;
        }

        if (usageSet.Count >= usageSet.Capacity)
        {
            entryIndex = -1;
            isAliased = false;

            return false;
        }

        entryIndex = usageSet.Count;

        storage[checked(usageSet.StorageOffset + entryIndex)] = new GraphicsResourceUsageEntry
        {
            Set = set,
            ResourceIndex = resourceIndex,
            Generation = generation,
            Access = observedAccess,
            FirstState = firstState,
            FinalState = finalState
        };

        usageSet.Count++;
        isAliased = false;

        return true;
    }

    public static Span<GraphicsResourceUsageEntry> GetEntries(Span<GraphicsResourceUsageEntry> storage, in UsageSetPoolEntry usageSet)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegative(usageSet.StorageOffset);
        default(ArgumentOutOfRangeException).ThrowIfNegative(usageSet.Count);
        default(ArgumentOutOfRangeException).ThrowIfGreaterThan(usageSet.Count, usageSet.Capacity);

        return storage.Slice(usageSet.StorageOffset, usageSet.Count);
    }

    public static void ClearUsages(Span<GraphicsResourceUsageEntry> storage, ref UsageSetPoolEntry usageSet)
    {
        GetEntries(storage, in usageSet).Clear();

        usageSet.Count = 0;
    }

    private static int ToBits(ComputeResourceAccess access)
    {
        return access switch
        {
            ComputeResourceAccess.Read => ReadBit,
            ComputeResourceAccess.Write => WriteBit,
            ComputeResourceAccess.ReadWrite => ReadBit | WriteBit,
            _ => throw new ArgumentOutOfRangeException(nameof(access))
        };
    }

    private static ComputeResourceAccess FromBits(int bits)
    {
        return bits switch
        {
            ReadBit => ComputeResourceAccess.Read,
            WriteBit => ComputeResourceAccess.Write,
            _ => ComputeResourceAccess.ReadWrite
        };
    }
}
