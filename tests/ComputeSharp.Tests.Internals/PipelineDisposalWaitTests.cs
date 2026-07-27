using System;
using System.Diagnostics;
using System.Threading;
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
    private const int TimeoutMilliseconds = 10_000;

    private const int BufferLength = 1 << 21;

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
            ReadWriteBuffer<int> buffer = this.binding.Resource!;

            context.For(buffer.Length, new ComputePipelineInvokerTests.AddShader(buffer, 1));
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

        SubmitOverTheOwnedGeneration(host);

        host.Dispose();
        host.WaitForDisposal();

        Assert.IsTrue(((IComputeOwnedSlot)slot).IsDisposalComplete);
        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

        slot.WaitForDisposal();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheOwnedSlotWithoutAnyFurtherCallAfterTheSubmissionIsDrained(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = CreateHost(graphicsDevice, slot);

        SubmitOverTheOwnedGeneration(host);

        host.Dispose();

        Assert.IsFalse(((IComputeOwnedSlot)slot).IsDisposalComplete, "The submission completed before the host was disposed, so the deferred release was not exercised.");

        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!((IComputeOwnedSlot)slot).IsDisposalComplete)
        {
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < TimeoutMilliseconds, "The owned slot of the host was not released.");

            Thread.Sleep(1);
        }
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

    private static void SubmitOverTheOwnedGeneration(ComputeHostRuntime host)
    {
        Assert.IsTrue(host.TryEnsureResource(
            0,
            [BufferLength],
            new ComputeHostRuntimeTests.BufferMaterializer(BufferLength),
            out _));

        ComputeResourceBinding<ReadWriteBuffer<int>> binding = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

        Assert.IsTrue(binding.IsValid);

        _ = host.Submit(new SlotFillInvocation(binding));
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
