using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class PipelineDisposalWaitTests
{
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
            context.For(64, new ComputePipelineInvokerTests.AddShader(this.binding.Resource!, 1));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheHostRegistrationWhenTheCommittedSubmissionIsDrained(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);
        CompletionRegistry completion = new();

        try
        {
            PipelineHostRuntime host = RegisterHost(registry);
            ComputeSubmission submission = SubmitOne(device, host, completion);

            host.RequestDispose();

            Assert.IsFalse(registry.TryUnregisterHost(host));
            Assert.AreEqual(RegistrationState.DisposeRequested, host.State);

            submission.Wait();

            Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(device.Get(), completion));
            Assert.AreEqual(RegistrationState.Released, host.State);
            Assert.AreEqual(0, registry.HostCount);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheHostOfACommittedSubmissionWhenTheRegistryIsDisposed(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get(), D3D12_COMMAND_LIST_TYPE_COMPUTE);
        PipelineHostRuntime host = RegisterHost(registry);

        _ = SubmitOne(device, host, registry.Completions);

        registry.Dispose();

        Assert.AreEqual(RegistrationState.Released, host.State);
        Assert.AreEqual(host.PendingRecords.Capacity, host.PendingRecords.AvailableCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReturnsFromWaitForDisposalOnlyAfterEveryOwnedGenerationIsReleased(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        ulong before = GetOwnedBytes(graphicsDevice);

        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = CreateHost(graphicsDevice, slot);

        Assert.IsTrue(host.TryEnsureResource(0, [64], new ComputeHostRuntimeTests.BufferMaterializer(64), out _));

        ComputeResourceBinding<ReadWriteBuffer<int>> binding = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

        Assert.IsTrue(binding.IsValid);

        _ = host.Submit(new SlotFillInvocation(binding));

        host.Dispose();
        host.WaitForDisposal();

        Assert.IsTrue(((IComputeOwnedSlot)slot).IsDisposalComplete);
        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

        slot.WaitForDisposal();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsWaitingForDisposalBeforeDisposalIsRequested(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = CreateHost(graphicsDevice, slot);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(host.WaitForDisposal);
            _ = Assert.ThrowsExactly<InvalidOperationException>(slot.WaitForDisposal);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    private static ComputeHostRuntime CreateHost(GraphicsDevice device, ComputeResourceSlot<ReadWriteBuffer<int>> slot)
    {
        return ComputeHostRuntime.Create(device, ComputeHostRuntimeTests.CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);
    }

    private static PipelineHostRuntime RegisterHost(DeviceRegistrationRegistry registry)
    {
        return registry.RegisterHost(
            DeviceRegistrationRegistryTests.CreateHostDescriptor(1),
            1,
            [new ComputeResourceSlot<ReadWriteBuffer<int>>()]);
    }

    private static ComputeSubmission SubmitOne(Device device, PipelineHostRuntime host, CompletionRegistry completion)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));

        ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());

        host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        _ = d3D12CommandList->Close();

        SubmissionRetention retention = new() { ResourceUsages = host.GetUsageSetHandle(index) };

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

        return ComputeSubmissionExecutor.Submit(device.Get(), host, completion, index, bundleIndex: 0, ref retention);
    }

    private static ulong GetOwnedBytes(GraphicsDevice device)
    {
        GraphicsMemoryStatistics statistics = device.GetMemoryStatistics();

        return statistics.Local.ComputeSharpOwnedBytes + statistics.NonLocal.ComputeSharpOwnedBytes;
    }
}
