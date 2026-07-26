using System;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Tests.Internals.Helpers;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ResourceUsageRecorderTests
{
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct CopyShader : IComputeShader
    {
        public readonly ReadOnlyBuffer<int> source;

        public readonly ReadWriteBuffer<int> destination;

        public void Execute()
        {
            this.destination[ThreadIds.X] = this.source[ThreadIds.X];
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AliasedShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> first;

        public readonly ReadWriteBuffer<int> second;

        public void Execute()
        {
            this.first[ThreadIds.X] = this.second[ThreadIds.X] + 1;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct VolumeShader : IComputeShader
    {
        public readonly ReadOnlyTexture1D<float> line;

        public readonly ReadOnlyTexture3D<float> volume;

        public readonly ReadWriteBuffer<int> destination;

        public void Execute()
        {
            this.destination[ThreadIds.X] = (int)(this.line[ThreadIds.X] + this.volume[ThreadIds.X, 0, 0]);
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SolidColorShader : IComputeShader<float4>
    {
        public float4 Execute()
        {
            return float4.UnitX;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsTheObservedAccessOfEveryBoundResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 2);

        using ReadOnlyBuffer<int> source = graphicsDevice.AllocateReadOnlyBuffer<int>(64);
        using ReadWriteBuffer<int> destination = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.For(64, new CopyShader(source, destination));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(2, usages.Length);

            AssertUsage(usages, source, ComputeResourceAccess.Read, TrackedResourceState.Common);
            AssertUsage(usages, destination, ComputeResourceAccess.ReadWrite, TrackedResourceState.Common);

            Discard(host, index, ref record, d3D12CommandList, resourceLeases);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DeduplicatesTheSameGenerationBoundToSeveralOrdinals(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.For(64, new AliasedShader(buffer, buffer));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(1, usages.Length);

            AssertUsage(usages, buffer, ComputeResourceAccess.ReadWrite, TrackedResourceState.Common);

            Discard(host, index, ref record, d3D12CommandList, resourceLeases);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsTheImplicitTargetTextureOfAPixelShader(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteTexture2D<Rgba32, float4> texture = graphicsDevice.AllocateReadWriteTexture2D<Rgba32, float4>(8, 8);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.ForEach<SolidColorShader, float4>(texture);

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(1, usages.Length);

            AssertUsage(usages, texture, ComputeResourceAccess.ReadWrite, TrackedResourceState.UnorderedAccess);

            Discard(host, index, ref record, d3D12CommandList, resourceLeases);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsTheObservedAccessOfEveryTextureRank(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 3);

        using ReadOnlyTexture1D<float> line = graphicsDevice.AllocateReadOnlyTexture1D<float>(8);
        using ReadOnlyTexture3D<float> volume = graphicsDevice.AllocateReadOnlyTexture3D<float>(8, 8, 8);
        using ReadWriteBuffer<int> destination = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.For(64, new VolumeShader(line, volume, destination));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(3, usages.Length);

            AssertUsage(usages, line, ComputeResourceAccess.Read, TrackedResourceState.Common);
            AssertUsage(usages, volume, ComputeResourceAccess.Read, TrackedResourceState.Common);
            AssertUsage(usages, destination, ComputeResourceAccess.ReadWrite, TrackedResourceState.Common);

            Discard(host, index, ref record, d3D12CommandList, resourceLeases);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsABoundResourceWithoutAGenerationIdentity(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ResourceUsageRecorder recorder = host.CreateUsageRecorder(index);

            _ = Assert.ThrowsExactly<ArgumentException>(() => recorder.Record(new UntrackedResource(graphicsDevice)));
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.Record(new UnboundResource(graphicsDevice)));

            Assert.AreEqual(0, GetUsages(host, index).Length);
            Assert.IsTrue(record.TryAbort());

            host.ReturnPendingRecord(index);
        }
        finally
        {
            registry.Dispose();
        }
    }

    private sealed class UntrackedResource(GraphicsDevice device) : IGraphicsResource
    {
        public GraphicsDevice GraphicsDevice { get; } = device;
    }

    private sealed class UnboundResource(GraphicsDevice device) : IGraphicsResource, IGenerationBoundResource
    {
        public GraphicsDevice GraphicsDevice { get; } = device;

        void IGenerationBoundResource.BindGeneration(IResourceGenerationOwner owner, int resourceIndex)
        {
            throw new NotSupportedException();
        }

        bool IGenerationBoundResource.TryGetGenerationBinding(out ResourceUsageBinding binding)
        {
            binding = default;

            return false;
        }
    }

    private static int Checkout(PipelineHostRuntime host)
    {
        PipelineKey pipeline = new(host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(host.TryCheckoutPendingRecord(pipeline, 1, out int index));
        Assert.IsTrue(host.PendingRecords.GetRecord(index).TryBeginRecording());

        return index;
    }

    private static ComputeContext Borrow(
        GraphicsDevice graphicsDevice,
        PipelineHostRuntime host,
        int index,
        out ID3D12GraphicsCommandList* d3D12CommandList)
    {
        host.CommandLists.Rent(null, out d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        return graphicsDevice.CreatePipelineComputeContext(
            d3D12CommandList,
            d3D12CommandAllocator,
            host.CreateUsageRecorder(index));
    }

    private static void Discard(
        PipelineHostRuntime host,
        int index,
        ref PendingSubmissionRecord record,
        ID3D12GraphicsCommandList* d3D12CommandList,
        GraphicsResourceLeaseSet? resourceLeases)
    {
        ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(index));

        resourceLeases?.Release();

        host.CommandLists.Return(d3D12CommandList, isCommandListClosed: true);

        Assert.IsTrue(record.TryAbort());

        host.ReturnPendingRecord(index);
    }

    private static GraphicsResourceUsageEntry[] GetUsages(PipelineHostRuntime host, int index)
    {
        return ResourceUsageTracker.GetEntries(host.UsageSets.Storage, in host.UsageSets.GetSet(index)).ToArray();
    }

    private static void AssertUsage(
        GraphicsResourceUsageEntry[] usages,
        IGraphicsResource resource,
        ComputeResourceAccess access,
        TrackedResourceState residentState)
    {
        Assert.IsTrue(((IGenerationBoundResource)resource).TryGetGenerationBinding(out ResourceUsageBinding binding));

        foreach (GraphicsResourceUsageEntry usage in usages)
        {
            if (usage.Generation != binding.Generation)
            {
                continue;
            }

            Assert.AreSame(binding.Set.Owner, usage.Set.Owner);
            Assert.AreEqual(binding.Set.SetId.Value, usage.Set.SetId.Value);
            Assert.AreEqual(binding.ResourceIndex, usage.ResourceIndex);
            Assert.AreEqual(access, usage.Access);
            Assert.AreEqual(residentState, usage.FirstState);
            Assert.AreEqual(residentState, usage.FinalState);

            return;
        }

        Assert.Fail("The bound resource has not been tracked.");
    }
}
