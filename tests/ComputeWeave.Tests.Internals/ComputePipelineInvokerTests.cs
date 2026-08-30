using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Tests.Internals.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public partial class ComputePipelineInvokerTests
{
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AddShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public readonly int offset;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = ThreadIds.X + this.offset;
        }
    }

    private readonly struct FillInvocation(ReadWriteBuffer<int> buffer, int offset) : IComputePipelineInvocation
    {
        private readonly ReadWriteBuffer<int> buffer = buffer;

        private readonly int offset = offset;

        public static int PipelineOrdinal => 0;

        public void Bind(ref ComputePipelineBinder binder)
        {
            Assert.IsTrue(binder.TryPin(this.buffer));
        }

        public void Record(in ComputeContext context)
        {
            context.For(64, new AddShader(this.buffer, this.offset));
        }
    }

    private readonly struct UnpinnedInvocation(ReadWriteBuffer<int> buffer) : IComputePipelineInvocation
    {
        private readonly ReadWriteBuffer<int> buffer = buffer;

        public static int PipelineOrdinal => 0;

        public void Bind(ref ComputePipelineBinder binder)
        {
        }

        public void Record(in ComputeContext context)
        {
            context.For(64, new AddShader(this.buffer, 1));
        }
    }

    private readonly struct SlotFillInvocation(ComputeResourceBinding<ReadWriteBuffer<int>> binding) : IComputePipelineInvocation
    {
        private readonly ComputeResourceBinding<ReadWriteBuffer<int>> binding = binding;

        public static int PipelineOrdinal => 0;

        public void Bind(ref ComputePipelineBinder binder)
        {
            Assert.IsTrue(binder.TryPin(0, in this.binding));
        }

        public void Record(in ComputeContext context)
        {
            context.For(64, new AddShader(this.binding.Resource!, 1));
        }
    }

    private readonly struct FailingInvocation(ReadWriteBuffer<int> buffer) : IComputePipelineInvocation
    {
        private readonly ReadWriteBuffer<int> buffer = buffer;

        public static int PipelineOrdinal => 0;

        public void Bind(ref ComputePipelineBinder binder)
        {
            Assert.IsTrue(binder.TryPin(this.buffer));
        }

        public void Record(in ComputeContext context)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// An invocation that must never be reached. The permit is checked before anything is bound or
    /// recorded, so a call into either member would mean the refusal came from somewhere else.
    /// </summary>
    private readonly struct RefusedInvocation : IComputePipelineInvocation
    {
        public static int PipelineOrdinal => 0;

        public void Bind(ref ComputePipelineBinder binder)
        {
            Assert.Fail("The invocation was bound after the permit was refused.");
        }

        public void Record(in ComputeContext context)
        {
            Assert.Fail("The invocation was recorded after the permit was refused.");
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheSlotOwnedGenerationAfterTheSubmissionCompletes(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            ComputeHostRuntimeTests.CreateDescriptor(ResourcePlanKind.Buffer),
            1,
            [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [64], new ComputeHostRuntimeTests.BufferMaterializer(64), out _));

            ComputeResourceBinding<ReadWriteBuffer<int>> binding = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.IsTrue(binding.IsValid);

            ComputeSubmission submission = host.Submit(new SlotFillInvocation(binding));

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        }
        finally
        {
            slot.Dispose();

            PipelineInvocationSetup.Release(host);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsAndSubmitsAGeneratedInvocation(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeHostRuntime host = PipelineInvocationSetup.Host(graphicsDevice);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            ComputeSubmission submission = host.Submit(new FillInvocation(buffer, 1));

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);

            int[] data = buffer.ToArray();

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(i + 1, data[i]);
            }

            ComputeSubmission second = host.Submit(new FillInvocation(buffer, 100));

            second.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, second.Status);
            Assert.AreEqual(100, buffer.ToArray()[0]);
        }
        finally
        {
            PipelineInvocationSetup.Release(host);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAnInvocationThatBindsAnUnpinnedResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeHostRuntime host = PipelineInvocationSetup.Host(graphicsDevice);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => host.Submit(new UnpinnedInvocation(buffer)));

            ComputeSubmission submission = host.Submit(new FillInvocation(buffer, 7));

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        }
        finally
        {
            PipelineInvocationSetup.Release(host);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEverythingWhenTheRecordedMethodThrows(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeHostRuntime host = PipelineInvocationSetup.Host(graphicsDevice);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            _ = Assert.ThrowsExactly<NotSupportedException>(() => host.Submit(new FailingInvocation(buffer)));

            ComputeSubmission submission = host.Submit(new FillInvocation(buffer, 3));

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.AreEqual(3, buffer.ToArray()[0]);
        }
        finally
        {
            PipelineInvocationSetup.Release(host);
        }
    }

    /// <summary>
    /// A host that has no concurrent invocation permit left refuses the next submission with its own
    /// identifier, before the invocation is bound or recorded.
    /// </summary>
    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesASubmissionWhenNoInvocationPermitIsLeft(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry);

        try
        {
            int acquired = 0;

            while (host.TryAcquireInvocation())
            {
                acquired++;

                Assert.IsTrue(acquired < 64, "The host never ran out of invocation permits.");
            }

            Assert.AreNotEqual(0, acquired);

            RefusedInvocation invocation = default;

            ComputeDiagnosticException rejection = Assert.ThrowsExactly<ComputeDiagnosticException>(
                () => ComputePipelineInvoker.Submit(registry, host, 0, in invocation));

            Assert.AreEqual("CMPW1004", rejection.DiagnosticId);

            for (int i = 0; i < acquired; i++)
            {
                host.ReleaseInvocation();
            }
        }
        finally
        {
            registry.Dispose();
        }
    }
}
