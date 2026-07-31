using ComputeSharp.Graphics.Pipelines;

namespace ComputeSharp.Resources.Lifetime;

internal readonly struct SlotTrimCandidate(
    ComputeResourceRecovery recovery,
    ulong lastUseSequence,
    ulong reclaimableBytes,
    ResourceId tieBreakResourceId)
{
    public ComputeResourceRecovery Recovery { get; } = recovery;

    public ulong LastUseSequence { get; } = lastUseSequence;

    public ulong ReclaimableBytes { get; } = reclaimableBytes;

    public ResourceId TieBreakResourceId { get; } = tieBreakResourceId;

    public static int Compare(SlotTrimCandidate left, SlotTrimCandidate right)
    {
        int order = left.LastUseSequence.CompareTo(right.LastUseSequence);

        if (order != 0)
        {
            return order;
        }

        order = right.ReclaimableBytes.CompareTo(left.ReclaimableBytes);

        if (order != 0)
        {
            return order;
        }

        return left.TieBreakResourceId.Value.CompareTo(right.TieBreakResourceId.Value);
    }
}
