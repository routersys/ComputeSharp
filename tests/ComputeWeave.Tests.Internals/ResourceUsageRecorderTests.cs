using System;
using ComputeWeave.Graphics.Commands;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Tests.Internals.Helpers;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

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
    public void RecordsClearedAndFilledResourcesAsWritten(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 2);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteTexture2D<Rgba32, float4> texture = graphicsDevice.AllocateReadWriteTexture2D<Rgba32, float4>(8, 8);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.Clear(buffer);
            context.Fill(texture, new float4(1, 0, 0, 1));

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(2, usages.Length);

            AssertUsage(usages, buffer, ComputeResourceAccess.Write, TrackedResourceState.Common);
            AssertUsage(usages, texture, ComputeResourceAccess.Write, TrackedResourceState.UnorderedAccess);

            Discard(host, index, ref record, d3D12CommandList, resourceLeases);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void UnionsTheAccessOfAClearedAndDispatchedResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            context.Clear(buffer);
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
    public void RecordsAReadOnlyViewUnderTheGenerationOfItsOwner(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteTexture2D<float> texture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        try
        {
            using (ComputeContext transitionContext = graphicsDevice.CreateComputeContext())
            {
                transitionContext.Transition(texture, ResourceState.ReadOnly);
            }

            IReadOnlyTexture2D<float> view = texture.AsReadOnly();

            Assert.IsTrue(((IGenerationBoundResource)texture).TryGetGenerationBinding(out ResourceUsageBinding owner));
            Assert.IsTrue(((IGenerationBoundResource)view).TryGetGenerationBinding(out ResourceUsageBinding viewBinding));

            Assert.AreSame(owner.Set.Owner, viewBinding.Set.Owner);
            Assert.AreEqual(owner.Generation, viewBinding.Generation);
            Assert.AreEqual(ComputeResourceAccess.ReadWrite, owner.Access);
            Assert.AreEqual(ComputeResourceAccess.Read, viewBinding.Access);
            Assert.AreEqual(TrackedResourceState.NonPixelShaderResource, viewBinding.ResidentState);
            Assert.AreEqual(TrackedResourceState.NonPixelShaderResource, owner.Set.Owner.GetResourceRecord(0).D3D12State);

            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            host.CreateUsageRecorder(index).Record(view);

            GraphicsResourceUsageEntry[] usages = GetUsages(host, index);

            Assert.AreEqual(1, usages.Length);
            Assert.AreEqual(owner.Generation, usages[0].Generation);
            Assert.AreEqual(ComputeResourceAccess.Read, usages[0].Access);
            Assert.AreEqual(TrackedResourceState.NonPixelShaderResource, usages[0].FirstState);

            ResourceUsageTracker.ClearUsages(host.UsageSets.Storage, ref host.UsageSets.GetSet(index));

            Assert.IsTrue(record.TryAbort());

            host.ReturnPendingRecord(index);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAStateTransitionWhileRecordingAPipeline(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteTexture2D<float> texture = graphicsDevice.AllocateReadWriteTexture2D<float>(8, 8);

        try
        {
            int index = Checkout(host);

            ref PendingSubmissionRecord record = ref host.PendingRecords.GetRecord(index);

            ComputeContext context = Borrow(graphicsDevice, host, index, out ID3D12GraphicsCommandList* d3D12CommandList);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => context.Transition(texture, ResourceState.ReadOnly));

            Assert.AreEqual(
                TrackedResourceState.UnorderedAccess,
                ((IResourceGenerationOwner)texture).GetResourceRecord(0).D3D12State);

            context.EndPipelineRecording(out GraphicsResourceLeaseSet? resourceLeases);

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
