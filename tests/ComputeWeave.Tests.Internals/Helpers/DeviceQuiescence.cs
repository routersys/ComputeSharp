using ComputeWeave.Graphics.Pipelines;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Waits for the asynchronous completion processing of a device to settle.
/// </summary>
/// <remarks>
/// <para>
/// Waiting for a submission only waits for its GPU fence. The pending submission references the submission
/// held are released by the completion coordinator thread, so a test that observes device wide generation
/// counts right after <c>Wait</c> races that thread.
/// </para>
/// <para>
/// Section 26.11 of the pipeline interop specification makes <c>PendingSubmissionReference == 0</c> part of the
/// idle condition, so a generation whose references the coordinator has not released yet is not a trim
/// candidate. Waiting here is therefore required by the contract rather than a workaround for it.
/// </para>
/// </remarks>
internal static class DeviceQuiescence
{
    /// <summary>
    /// Waits until the completion coordinator of a device has run a full drain pass started after the call.
    /// </summary>
    /// <param name="device">The device to wait for.</param>
    /// <remarks>
    /// Two passes are awaited rather than one. The first may already have been running when the call started,
    /// so only the second is guaranteed to observe everything the caller completed. The wait never depends on
    /// the registry becoming empty, because other tests on the same device hold committed submissions on
    /// purpose and their records would never drain.
    /// </remarks>
    public static void WaitForCompletionQuiescence(this GraphicsDevice device)
    {
        if (device.TryGetRegistrationRegistry() is not DeviceRegistrationRegistry registry)
        {
            return;
        }

        CompletionCoordinator coordinator = registry.Coordinator;

        for (int pass = 0; pass < 2; pass++)
        {
            ulong progress = coordinator.ProgressVersion;

            coordinator.Wake();

            if (!coordinator.TryWaitForProgress(progress))
            {
                return;
            }
        }
    }
}
