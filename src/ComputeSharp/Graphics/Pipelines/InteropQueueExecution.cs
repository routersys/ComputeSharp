using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal readonly struct InteropQueueExecution
{
    public bool IsExecutionIssued { get; init; }

    public FencePoint Completion { get; init; }

    public HRESULT Result { get; init; }

    public string FailedOperation { get; init; }

    public bool IsSequenceExhausted { get; init; }
}
