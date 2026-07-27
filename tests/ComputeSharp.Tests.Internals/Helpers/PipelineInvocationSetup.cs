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
        host.Dispose();
        host.WaitForDisposal();
    }
}
