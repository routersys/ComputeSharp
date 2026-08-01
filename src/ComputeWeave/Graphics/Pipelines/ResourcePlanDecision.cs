namespace ComputeWeave.Graphics.Pipelines;

internal enum ResourcePlanDecision : byte
{
    Identical = 0,
    LogicalUpdate = 1,
    Replacement = 2
}
