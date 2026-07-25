namespace ComputeSharp;

/// <summary>
/// A scoped operation that holds the external queue ownership of a compute interop domain.
/// </summary>
public readonly ref struct ExternalQueueOperation
{
    /// <summary>
    /// Gets whether the current operation still holds the external queue ownership.
    /// </summary>
    public bool IsValid => false;

    /// <summary>
    /// Releases the external queue ownership held by the current operation.
    /// </summary>
    public void Dispose()
    {
    }
}
