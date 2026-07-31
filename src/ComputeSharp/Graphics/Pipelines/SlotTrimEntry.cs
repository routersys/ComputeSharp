using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly struct SlotTrimEntry(PipelineHostRuntime host, IComputeOwnedSlot slot, in SlotTrimCandidate candidate)
{
    public PipelineHostRuntime Host { get; } = host;

    public IComputeOwnedSlot Slot { get; } = slot;

    public SlotTrimCandidate Candidate { get; } = candidate;
}
