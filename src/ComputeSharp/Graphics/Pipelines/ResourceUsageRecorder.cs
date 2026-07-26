using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly struct ResourceUsageRecorder
{
    private readonly ResourceUsageSetPartition? usageSets;

    private readonly int setIndex;

    public ResourceUsageRecorder(ResourceUsageSetPartition usageSets, UsageSetHandle usages)
    {
        default(ArgumentNullException).ThrowIfNull(usageSets);

        this.usageSets = usageSets;
        this.setIndex = usageSets.GetSetIndex(usages);
    }

    public bool IsRecording => this.usageSets is not null;

    public void Record(IGraphicsResource resource)
    {
        Record(resource, null);
    }

    public void RecordWrite(IGraphicsResource resource)
    {
        Record(resource, ComputeResourceAccess.Write);
    }

    private void Record(IGraphicsResource resource, ComputeResourceAccess? observedAccess)
    {
        if (this.usageSets is not ResourceUsageSetPartition usageSets)
        {
            return;
        }

        default(ArgumentNullException).ThrowIfNull(resource);

        if (resource is not IGenerationBoundResource boundResource)
        {
            default(ArgumentException).Throw(nameof(resource));

            return;
        }

        default(InvalidOperationException).ThrowIf(
            !boundResource.TryGetGenerationBinding(out ResourceUsageBinding binding),
            "The bound resource carries no generation identity to track its usage with.");

        bool isTracked = ResourceUsageTracker.TryAddUsage(
            usageSets.Storage,
            ref usageSets.GetSet(this.setIndex),
            binding.Set,
            binding.ResourceIndex,
            binding.Generation,
            observedAccess ?? binding.Access,
            binding.ResidentState,
            binding.ResidentState,
            out _,
            out _);

        default(InvalidOperationException).ThrowIf(!isTracked, "The resource usage set of the submission has no entry left.");
    }
}
