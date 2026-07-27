using System;
using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal static class SlotDisposalWait
{
    public static void Run(ref SlotGate gate, DeviceRegistrationRegistry registry, string notDisposedMessage)
    {
        default(ArgumentNullException).ThrowIfNull(registry);

        while (!gate.IsDisposalComplete)
        {
            default(InvalidOperationException).ThrowIf(!gate.IsDisposeRequested, notDisposedMessage);

            ulong progress = registry.Coordinator.ProgressVersion;

            SlotGenerationMaintenance.Run(ref gate);

            if (gate.IsDisposalComplete)
            {
                return;
            }

            default(InvalidOperationException).ThrowIf(
                !registry.Coordinator.TryWaitForProgress(progress),
                "The completion coordinator of the device stopped before the slot was released.");
        }
    }
}
