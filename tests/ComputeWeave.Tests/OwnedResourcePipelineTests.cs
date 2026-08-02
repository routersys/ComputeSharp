using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable IDE0051, IDE0060

namespace ComputeWeave.Tests;

[ComputeResourceGroup]
internal sealed partial class OwnedOperandResources
{
    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> Factors { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> Source { get; }
}

[ComputePipelineHost("device", 1)]
internal sealed partial class OwnedResourcePipelineHost
{
    private readonly GraphicsDevice device;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> destination = new();

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceGroupSlot<OwnedOperandResources> operands = new();

    [ComputePipeline]
    private void Prepare(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(operands))] OwnedOperandResources operands,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> seed,
        int length)
    {
        _ = this.device;

        context.For(length, new OwnedResourcePipelineTests.PrepareShader(seed, operands.Source, operands.Factors));
    }

    [ComputePipeline]
    private void Multiply(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(destination))] ReadWriteBuffer<int> destination,
        [ComputeOwnedResource(nameof(operands))] OwnedOperandResources operands,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> output,
        int length)
    {
        _ = this.device;

        context.For(length, new OwnedResourcePipelineTests.MultiplyShader(operands.Source, operands.Factors, destination, output));
    }
}

[TestClass]
public partial class OwnedResourcePipelineTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Dispatch_OwnedResourcesReachThePipelineBody(Device device)
    {
        const int length = 64;

        GraphicsDevice graphicsDevice = device.Get();
        OwnedResourcePipelineHost host = OwnedResourcePipelineHost.Create(graphicsDevice, 3);

        int[] seed = new int[length];

        for (int i = 0; i < length; i++)
        {
            seed[i] = i + 1;
        }

        try
        {
            using ReadWriteBuffer<int> seedBuffer = graphicsDevice.AllocateReadWriteBuffer(seed);
            using ReadWriteBuffer<int> outputBuffer = graphicsDevice.AllocateReadWriteBuffer<int>(length);

            Assert.IsTrue(host.TryEnsureDestination(new OwnedResourcePipelineHost.DestinationPlan(length), out _));
            Assert.IsTrue(host.TryEnsureOperands(new OwnedOperandResources.Plan(length, length), out _));

            host.Prepare(seedBuffer, length).Wait();
            host.Multiply(outputBuffer, length).Wait();

            int[] results = outputBuffer.ToArray();

            for (int i = 0; i < length; i++)
            {
                Assert.AreEqual(seed[i] * (seed[i] + 1), results[i]);
            }
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Dispatch_SubmittingAPipelineDoesNotAllocateManagedMemory(Device device)
    {
        const int length = 64;

        GraphicsDevice graphicsDevice = device.Get();
        OwnedResourcePipelineHost host = OwnedResourcePipelineHost.Create(graphicsDevice, 3);

        try
        {
            using ReadWriteBuffer<int> seedBuffer = graphicsDevice.AllocateReadWriteBuffer<int>(length);

            Assert.IsTrue(host.TryEnsureDestination(new OwnedResourcePipelineHost.DestinationPlan(length), out _));
            Assert.IsTrue(host.TryEnsureOperands(new OwnedOperandResources.Plan(length, length), out _));

            for (int i = 0; i < 4; i++)
            {
                host.Prepare(seedBuffer, length).Wait();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long minimum = long.MaxValue;

            for (int i = 0; i < 16; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();

                host.Prepare(seedBuffer, length).Wait();

                minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
            }

            Assert.AreEqual(0, minimum);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct PrepareShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> seed;
        public readonly ReadWriteBuffer<int> source;
        public readonly ReadWriteBuffer<int> factors;

        /// <inheritdoc/>
        public void Execute()
        {
            this.source[ThreadIds.X] = this.seed[ThreadIds.X];
            this.factors[ThreadIds.X] = this.seed[ThreadIds.X] + 1;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MultiplyShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> source;
        public readonly ReadWriteBuffer<int> factors;
        public readonly ReadWriteBuffer<int> destination;
        public readonly ReadWriteBuffer<int> output;

        /// <inheritdoc/>
        public void Execute()
        {
            this.destination[ThreadIds.X] = this.source[ThreadIds.X] * this.factors[ThreadIds.X];
            this.output[ThreadIds.X] = this.destination[ThreadIds.X];
        }
    }
}
