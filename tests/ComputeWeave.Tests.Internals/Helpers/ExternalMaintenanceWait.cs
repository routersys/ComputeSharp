using System;
using ComputeWeave.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Waits for the maintenance coordinator to reach a state a test wants to observe.
/// </summary>
/// <remarks>
/// <para>
/// Section 4.1 of the external drain maintenance specification reserves the execution of an external drain to
/// the coordinator. A requester such as <c>Dispose</c> or <c>TryEnsure</c> only wakes it, so a test that wants
/// to see the drain has to wait for the coordinator rather than assert right after the request.
/// </para>
/// <para>
/// The wait is driven by the progress the coordinator publishes, so it costs nothing while the coordinator is
/// working and never depends on wall clock time. It is bounded so that a state the coordinator never reaches
/// fails the test instead of hanging it.
/// </para>
/// </remarks>
internal static class ExternalMaintenanceWait
{
    private const int MaximumPasses = 1000;

    /// <summary>
    /// Waits until a condition holds, letting the coordinator run between each check.
    /// </summary>
    /// <param name="device">The device whose coordinator runs the maintenance.</param>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="expectation">What the caller is waiting for, used in the failure message.</param>
    public static void WaitFor(GraphicsDevice device, Func<bool> condition, string expectation)
    {
        if (condition())
        {
            return;
        }

        if (device.TryGetRegistrationRegistry() is not DeviceRegistrationRegistry registry)
        {
            Assert.Fail($"The device has no registration registry, so it never reaches: {expectation}");

            return;
        }

        CompletionCoordinator coordinator = registry.Coordinator;

        for (int pass = 0; pass < MaximumPasses; pass++)
        {
            ulong progress = coordinator.ProgressVersion;

            coordinator.Wake();

            if (!coordinator.TryWaitForProgress(progress))
            {
                break;
            }

            if (condition())
            {
                return;
            }
        }

        Assert.Fail($"The maintenance coordinator never reached: {expectation}");
    }
}
