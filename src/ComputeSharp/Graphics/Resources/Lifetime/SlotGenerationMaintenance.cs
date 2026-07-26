namespace ComputeSharp.Resources.Lifetime;

internal static class SlotGenerationMaintenance
{
    public static void Run(ref SlotGate gate)
    {
        while (RunPass(ref gate))
        {
        }
    }

    private static bool RunPass(ref SlotGate gate)
    {
        gate.GetMaintenanceHandles(
            out ResourceGenerationSetHandle active,
            out ResourceGenerationSetHandle prepared,
            out ResourceGenerationSetHandle retired,
            out bool isRetiringActive);

        bool hasProgressed = false;

        if (!retired.IsEmpty && TryReleaseRetired(in retired))
        {
            hasProgressed = gate.TryClearRetired(retired.SetId);
        }

        if (isRetiringActive)
        {
            _ = TryReleaseRetired(in active);
            _ = TryReleaseRetired(in prepared);

            hasProgressed |= gate.TryCompleteRetiringActive();
        }

        return hasProgressed;
    }

    private static bool TryReleaseRetired(in ResourceGenerationSetHandle handle)
    {
        if (handle.IsEmpty)
        {
            return true;
        }

        return handle.Owner is ResourceGenerationOwner owner &&
            owner.TryReleaseRetired(ResourceReleaseAuthority.NormalCompletion);
    }
}
