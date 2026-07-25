namespace ComputeSharp.Resources.Lifetime;

internal readonly struct SlotResourcePlanStateRecord(int storageOffset, int fieldCount)
{
    public int StorageOffset { get; } = storageOffset;

    public int FieldCount { get; } = fieldCount;
}
