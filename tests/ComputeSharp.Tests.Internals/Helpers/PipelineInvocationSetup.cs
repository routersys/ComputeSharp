using System;
using System.Diagnostics;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals.Helpers;

internal static class PipelineInvocationSetup
{
    public static ComputeHostRuntime Host(GraphicsDevice device, int parameterCount = 1)
    {
        return ComputeHostRuntime.Create(
            device,
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1, parameterCount),
            2,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    public static void Release(ComputeHostRuntime host)
    {
        CompletionRegistry completions = host.Device.GetRegistrationRegistry().Completions;
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (completions.CommittedCount != 0)
        {
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "The submissions of the host were not released.");

            Thread.Sleep(1);
        }

        host.Dispose();

        while (!TryCompleteDisposal(host))
        {
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "The host of the submissions was not released.");

            Thread.Sleep(1);
        }
    }

    private static bool TryCompleteDisposal(ComputeHostRuntime host)
    {
        try
        {
            host.WaitForDisposal();

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
