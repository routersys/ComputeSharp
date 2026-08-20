#if USE_D3D12MA
using ComputeWeave.D3D12MemoryAllocator;
#endif
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.DebugLayer;

/// <summary>
/// Tests that assert the conformance condition of Section 0.3, that is, that the D3D12 debug layer
/// and GPU-based validation report no error for the operations covered here. The library flushes the
/// info queue on every <c>Assert</c> when the debug output is enabled, and throws if any message was
/// logged, so an operation that trips the debug layer surfaces as a failing test.
/// </summary>
[TestClass]
public partial class DebugLayerConformanceTests
{
    [AssemblyInitialize]
    public static void ConfigureD3D12MemoryAllocator(TestContext _)
    {
#if USE_D3D12MA
        AllocationServices.ConfigureAllocatorFactory(new D3D12MemoryAllocatorFactory());
#endif
    }

    /// <summary>
    /// Drains the info queue of every device, so that a message logged by the last native call of a
    /// test is attributed to that test and does not leak into the next one.
    /// </summary>
    [TestCleanup]
    public void DrainTheInfoQueueOfEveryDevice()
    {
        foreach (GraphicsDevice device in GraphicsDevice.QueryDevices(static _ => true))
        {
            using ReadOnlyBuffer<float> buffer = device.AllocateReadOnlyBuffer<float>(4, AllocationMode.Clear);

            _ = buffer.ToArray();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CopiesBuffersWithoutADebugLayerError(Device device)
    {
        using ReadOnlyBuffer<float> readOnlySource = device.Get().AllocateReadOnlyBuffer<float>(256, AllocationMode.Clear);
        using ReadOnlyBuffer<float> readOnlyDestination = device.Get().AllocateReadOnlyBuffer<float>(256, AllocationMode.Clear);
        using ReadWriteBuffer<float> readWriteSource = device.Get().AllocateReadWriteBuffer<float>(256, AllocationMode.Clear);
        using ReadWriteBuffer<float> readWriteDestination = device.Get().AllocateReadWriteBuffer<float>(256, AllocationMode.Clear);

        readOnlySource.CopyTo(readOnlyDestination, 64, 0, 128);
        readWriteSource.CopyTo(readWriteDestination, 64, 0, 128);
        readOnlySource.CopyTo(readWriteDestination, 64, 0, 128);
        readWriteSource.CopyTo(readOnlyDestination, 64, 0, 128);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CopiesTexture1DRegionsWithoutADebugLayerError(Device device)
    {
        using ReadOnlyTexture1D<float> readOnlySource = device.Get().AllocateReadOnlyTexture1D<float>(64, AllocationMode.Clear);
        using ReadOnlyTexture1D<float> readOnlyDestination = device.Get().AllocateReadOnlyTexture1D<float>(64, AllocationMode.Clear);
        using ReadWriteTexture1D<float> readWriteDestination = device.Get().AllocateReadWriteTexture1D<float>(64, AllocationMode.Clear);

        readOnlySource.CopyTo(readOnlyDestination, 16, 0, 32);
        readOnlySource.CopyTo(readWriteDestination, 16, 0, 32);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CopiesTexture2DRegionsWithoutADebugLayerError(Device device)
    {
        using ReadOnlyTexture2D<float> readOnlySource = device.Get().AllocateReadOnlyTexture2D<float>(64, 64, AllocationMode.Clear);
        using ReadOnlyTexture2D<float> readOnlyDestination = device.Get().AllocateReadOnlyTexture2D<float>(64, 64, AllocationMode.Clear);
        using ReadWriteTexture2D<float> readWriteDestination = device.Get().AllocateReadWriteTexture2D<float>(64, 64, AllocationMode.Clear);

        readOnlySource.CopyTo(readOnlyDestination, 16, 16, 0, 0, 32, 32);
        readOnlySource.CopyTo(readWriteDestination, 16, 16, 0, 0, 32, 32);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void CopiesTexture3DRegionsWithoutADebugLayerError(Device device)
    {
        using ReadOnlyTexture3D<float> readOnlySource = device.Get().AllocateReadOnlyTexture3D<float>(32, 32, 4, AllocationMode.Clear);
        using ReadOnlyTexture3D<float> readOnlyDestination = device.Get().AllocateReadOnlyTexture3D<float>(32, 32, 4, AllocationMode.Clear);
        using ReadWriteTexture3D<float> readWriteDestination = device.Get().AllocateReadWriteTexture3D<float>(32, 32, 4, AllocationMode.Clear);

        readOnlySource.CopyTo(readOnlyDestination, 0, 0, 1, 0, 0, 0, 32, 32, 2);
        readOnlySource.CopyTo(readWriteDestination, 0, 0, 1, 0, 0, 0, 32, 32, 2);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RoundTripsBuffersThroughTransferResourcesWithoutADebugLayerError(Device device)
    {
        float[] values = new float[256];

        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(256, AllocationMode.Clear);
        using UploadBuffer<float> upload = device.Get().AllocateUploadBuffer<float>(256);
        using ReadBackBuffer<float> readBack = device.Get().AllocateReadBackBuffer<float>(256);

        upload.CopyTo(buffer);
        buffer.CopyTo(readBack);
        buffer.CopyTo(values);
        buffer.CopyFrom(values);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RoundTripsTexturesThroughTransferResourcesWithoutADebugLayerError(Device device)
    {
        float[] values = new float[64 * 64];

        using ReadWriteTexture2D<float> texture = device.Get().AllocateReadWriteTexture2D<float>(64, 64, AllocationMode.Clear);
        using ReadOnlyTexture2D<float> readOnlyTexture = device.Get().AllocateReadOnlyTexture2D<float>(64, 64, AllocationMode.Clear);
        using UploadTexture2D<float> upload = device.Get().AllocateUploadTexture2D<float>(64, 64);
        using ReadBackTexture2D<float> readBack = device.Get().AllocateReadBackTexture2D<float>(64, 64);

        upload.CopyTo(texture);
        texture.CopyTo(readBack);
        readOnlyTexture.CopyTo(readBack);
        readOnlyTexture.CopyTo(values);
        readOnlyTexture.CopyFrom(values);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DispatchesAShaderWithoutADebugLayerError(Device device)
    {
        using ReadOnlyBuffer<float> source = device.Get().AllocateReadOnlyBuffer<float>(256, AllocationMode.Clear);
        using ReadWriteBuffer<float> destination = device.Get().AllocateReadWriteBuffer<float>(256, AllocationMode.Clear);
        using ReadWriteTexture2D<float> texture = device.Get().AllocateReadWriteTexture2D<float>(64, 64, AllocationMode.Clear);

        device.Get().For(256, new AddOneShader(source, destination));

        using (ComputeContext context = device.Get().CreateComputeContext())
        {
            context.Clear(texture);
            context.For(256, new AddOneShader(source, destination));
            context.Transition(texture, ResourceState.ReadOnly);
        }

        _ = destination.ToArray();
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AddOneShader(ReadOnlyBuffer<float> source, ReadWriteBuffer<float> destination) : IComputeShader
    {
        public void Execute()
        {
            destination[ThreadIds.X] = source[ThreadIds.X] + 1;
        }
    }
}
