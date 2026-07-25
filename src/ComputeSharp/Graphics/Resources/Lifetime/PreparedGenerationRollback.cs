namespace ComputeSharp.Resources.Lifetime;

internal static class PreparedGenerationRollback
{
    public static void RollbackUnpublished(in ResourceGenerationSetHandle prepared)
    {
        if (prepared.IsEmpty)
        {
            return;
        }

        IResourceGenerationOwner owner = prepared.Owner;

        for (int i = owner.ResourceCount - 1; i >= 0; i--)
        {
            ref ResourceGenerationRecord record = ref owner.GetResourceRecord(i);

            _ = record.TryRequestRetire();
            _ = record.TryPromoteRetiredReady(record.RetirementFence.IsNone);
        }
    }
}
