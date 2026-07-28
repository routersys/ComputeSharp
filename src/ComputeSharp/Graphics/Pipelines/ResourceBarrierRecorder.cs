using System;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal static unsafe class ResourceBarrierRecorder
{
    public static void RecordBarriers(
        ID3D12GraphicsCommandList* d3D12GraphicsCommandList,
        Span<GraphicsResourceUsageEntry> usages,
        ReadOnlySpan<ResourceBarrierPlanEntry> plan)
    {
        default(ArgumentNullException).ThrowIf(d3D12GraphicsCommandList is null, nameof(d3D12GraphicsCommandList));
        default(ArgumentException).ThrowIf(plan.IsEmpty, nameof(plan));

        D3D12_RESOURCE_BARRIER* d3D12ResourceBarriers = stackalloc D3D12_RESOURCE_BARRIER[plan.Length];

        for (int i = 0; i < plan.Length; i++)
        {
            ref readonly ResourceBarrierPlanEntry entry = ref plan[i];

            default(ArgumentOutOfRangeException).ThrowIfNotInRange(entry.UsageIndex, 0, usages.Length);

            ref GraphicsResourceUsageEntry usage = ref usages[entry.UsageIndex];

            ID3D12Resource* d3D12Resource = usage.Set.Owner.GetResourceNativePointer(checked((int)usage.ResourceIndex));

            default(InvalidOperationException).ThrowIf(
                d3D12Resource is null,
                "The tracked resource generation has no native resource to insert a barrier for.");

            d3D12ResourceBarriers[i] = entry.Kind switch
            {
                ResourceBarrierKind.Transition => D3D12_RESOURCE_BARRIER.InitTransition(
                    d3D12Resource,
                    ComputeGenerationDescriber.GetD3D12ResourceStates(entry.BeforeState),
                    ComputeGenerationDescriber.GetD3D12ResourceStates(entry.AfterState)),
                ResourceBarrierKind.UnorderedAccess => D3D12_RESOURCE_BARRIER.InitUAV(d3D12Resource),
                _ => default(ArgumentException).Throw<D3D12_RESOURCE_BARRIER>(nameof(plan))
            };
        }

        d3D12GraphicsCommandList->ResourceBarrier((uint)plan.Length, d3D12ResourceBarriers);
    }
}
