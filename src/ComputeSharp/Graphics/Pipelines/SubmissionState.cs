namespace ComputeSharp.Graphics.Pipelines;

internal enum SubmissionState : byte
{
    Reserved = 0,
    Recording = 1,
    Prepared = 2,
    ExecutionIssued = 3,
    CompletionSignaled = 4,
    HazardCommitted = 5,
    Committed = 6,
    CompletionReady = 7,
    Returning = 8,
    Returned = 9,
    TerminalRetained = 10
}
