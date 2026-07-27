namespace ComputeSharp.Resources.Lifetime;

internal static class SlotTerminalRelease
{
    public static void Run(ref SlotGate gate)
    {
        _ = gate.TryMarkDeviceTerminal();

        gate.GetMaintenanceHandles(
            out ResourceGenerationSetHandle active,
            out ResourceGenerationSetHandle prepared,
            out ResourceGenerationSetHandle retired,
            out _);

        Release(in active);
        Release(in prepared);
        Release(in retired);

        _ = gate.TryCompleteRetiringActive();
    }

    private static void Release(in ResourceGenerationSetHandle handle)
    {
        if (handle.Owner is ResourceGenerationOwner owner)
        {
            _ = owner.TryReleaseRetired(ResourceReleaseAuthority.DeviceTeardown);
        }
    }
}
