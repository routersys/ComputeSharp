using System;
using ComputeSharp.Graphics.Commands;
using ComputeSharp.Resources.Interop;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly struct ResourceUsageRecorder
{
    private readonly ResourceUsageSetPartition? usageSets;

    private readonly GraphicsResourceLeaseSet? manualUsages;

    private readonly int setIndex;

    public ResourceUsageRecorder(ResourceUsageSetPartition usageSets, UsageSetHandle usages)
    {
        default(ArgumentNullException).ThrowIfNull(usageSets);

        this.usageSets = usageSets;
        this.manualUsages = null;
        this.setIndex = usageSets.GetSetIndex(usages);
    }

    public ResourceUsageRecorder(GraphicsResourceLeaseSet manualUsages)
    {
        default(ArgumentNullException).ThrowIfNull(manualUsages);

        this.usageSets = null;
        this.manualUsages = manualUsages;
        this.setIndex = 0;
    }

    public bool IsRecording => this.usageSets is not null || this.manualUsages is not null;

    public void Record(IGraphicsResource resource)
    {
        Record(resource, null);
    }

    public void RecordWrite(IGraphicsResource resource)
    {
        Record(resource, ComputeResourceAccess.Write);
    }

    public void RecordCopy(IGraphicsResource resource, ComputeResourceAccess access)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentException).ThrowIf(
            access is not ComputeResourceAccess.Read and not ComputeResourceAccess.Write,
            nameof(access));
        default(InvalidOperationException).ThrowIf(this.manualUsages is null);

        ResourceUsageBinding binding = GetBinding(resource);

        this.manualUsages!.RecordResourceUsage(
            in binding,
            access,
            TrackedResourceState.Common,
            TrackedResourceState.Common);

        if (access is ComputeResourceAccess.Write &&
            resource is ID3D12ReadWriteResource readWriteResource)
        {
            readWriteResource.SetReadOnlyViewAvailability(false);
        }
    }

    public TrackedResourceState RecordTransition(IGraphicsResource resource, TrackedResourceState finalState)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentException).ThrowIf(finalState is TrackedResourceState.Unknown, nameof(finalState));
        default(InvalidOperationException).ThrowIf(this.manualUsages is null);

        ResourceUsageBinding binding = GetBinding(resource);

        if (!this.manualUsages!.TryGetFinalState(binding.Generation, out TrackedResourceState firstState))
        {
            lock (resource.GraphicsDevice.HazardGate)
            {
                ref ResourceGenerationRecord record = ref binding.Set.Owner.GetResourceRecord(checked((int)binding.ResourceIndex));

                default(InvalidOperationException).ThrowIf(
                    record.Id != binding.Generation,
                    "The generation of the transitioned resource no longer matches its binding.");

                firstState = record.D3D12State;
            }
        }

        this.manualUsages.RecordResourceUsage(
            in binding,
            ComputeResourceAccess.ReadWrite,
            firstState,
            finalState);

        return firstState;
    }

    private void Record(IGraphicsResource resource, ComputeResourceAccess? observedAccess)
    {
        if (!IsRecording)
        {
            return;
        }

        default(ArgumentNullException).ThrowIfNull(resource);

        ResourceUsageBinding binding = GetBinding(resource);
        ComputeResourceAccess access = observedAccess ?? binding.Access;

        if (this.manualUsages is not null &&
            access is not ComputeResourceAccess.Read &&
            resource is ID3D12ReadWriteResource readWriteResource)
        {
            readWriteResource.SetReadOnlyViewAvailability(false);
        }

        if (this.manualUsages is GraphicsResourceLeaseSet manualUsages)
        {
            manualUsages.RecordResourceUsage(
                in binding,
                access,
                binding.ResidentState,
                binding.ResidentState);

            return;
        }

        ResourceUsageSetPartition usageSets = this.usageSets!;

        bool isTracked = ResourceUsageTracker.TryAddUsage(
            usageSets.Storage,
            ref usageSets.GetSet(this.setIndex),
            binding.Set,
            binding.ResourceIndex,
            binding.Generation,
            access,
            binding.ResidentState,
            binding.ResidentState,
            out _,
            out _);

        default(InvalidOperationException).ThrowIf(!isTracked, "The resource usage set of the submission has no entry left.");
    }

    private static ResourceUsageBinding GetBinding(IGraphicsResource resource)
    {
        if (resource is not IGenerationBoundResource boundResource)
        {
            return default(ArgumentException).Throw<ResourceUsageBinding>(nameof(resource));
        }

        default(InvalidOperationException).ThrowIf(
            !boundResource.TryGetGenerationBinding(out ResourceUsageBinding binding),
            "The bound resource carries no generation identity to track its usage with.");

        return binding;
    }
}
