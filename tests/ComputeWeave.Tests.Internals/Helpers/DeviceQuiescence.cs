using ComputeWeave.Graphics.Pipelines;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Releases the submissions a device has already completed.
/// </summary>
/// <remarks>
/// <para>
/// Waiting for a submission only waits for its GPU fence. The pending submission references the submission
/// held are released afterwards, so a test that observes device wide generation counts right after
/// <c>Wait</c> sees them only once that release has run.
/// </para>
/// <para>
/// Section 26.11 of the pipeline interop specification makes <c>PendingSubmissionReference == 0</c> part of the
/// idle condition, so a generation whose references nothing has released yet is not a trim candidate. Draining
/// here is therefore required by the contract rather than a workaround for it.
/// </para>
/// </remarks>
internal static class DeviceQuiescence
{
    /// <summary>
    /// Releases every submission of a device whose fence has completed.
    /// </summary>
    /// <param name="device">The device to drain.</param>
    /// <remarks>
    /// The caller thread runs the drain, which is the same claim the completion coordinator thread makes and is
    /// safe to run beside it. The coordinator is deliberately left alone. Waking it would run device wide
    /// external maintenance that the caller never asked for.
    /// </remarks>
    public static void DrainCompletedSubmissions(this GraphicsDevice device)
    {
        if (device.TryGetRegistrationRegistry() is not DeviceRegistrationRegistry registry)
        {
            return;
        }

        while (ComputeSubmissionExecutor.TryReleaseCompleted(device, registry.Completions))
        {
        }
    }
}
