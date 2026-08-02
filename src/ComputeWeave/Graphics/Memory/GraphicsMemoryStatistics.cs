namespace ComputeWeave;

/// <summary>
/// The memory statistics observed for a graphics device.
/// </summary>
public readonly struct GraphicsMemoryStatistics
{
    /// <summary>
    /// Creates a new <see cref="GraphicsMemoryStatistics"/> instance with the specified parameters.
    /// </summary>
    /// <param name="epoch">The observation epoch the statistics were taken at.</param>
    /// <param name="local">The statistics of the local memory segment.</param>
    /// <param name="nonLocal">The statistics of the non local memory segment.</param>
    /// <param name="activeGenerationCount">The number of published resource generations.</param>
    /// <param name="retiredGenerationCount">The number of retired resource generations that have not been released.</param>
    /// <param name="managedPoolSurplusCount">The number of managed pool entries that exceed the current demand.</param>
    /// <param name="nativeReferencedGenerationCount">The number of resource generations held by a native reference.</param>
    internal GraphicsMemoryStatistics(
        ulong epoch,
        GraphicsMemorySegmentStatistics local,
        GraphicsMemorySegmentStatistics nonLocal,
        int activeGenerationCount,
        int retiredGenerationCount,
        int managedPoolSurplusCount,
        int nativeReferencedGenerationCount)
    {
        Epoch = epoch;
        Local = local;
        NonLocal = nonLocal;
        ActiveGenerationCount = activeGenerationCount;
        RetiredGenerationCount = retiredGenerationCount;
        ManagedPoolSurplusCount = managedPoolSurplusCount;
        NativeReferencedGenerationCount = nativeReferencedGenerationCount;
    }

    /// <summary>
    /// Gets the observation epoch the statistics were taken at.
    /// </summary>
    public ulong Epoch { get; }

    /// <summary>
    /// Gets the statistics of the local memory segment.
    /// </summary>
    public GraphicsMemorySegmentStatistics Local { get; }

    /// <summary>
    /// Gets the statistics of the non local memory segment.
    /// </summary>
    public GraphicsMemorySegmentStatistics NonLocal { get; }

    /// <summary>
    /// Gets the number of published resource generations.
    /// </summary>
    public int ActiveGenerationCount { get; }

    /// <summary>
    /// Gets the number of retired resource generations that have not been released.
    /// </summary>
    public int RetiredGenerationCount { get; }

    /// <summary>
    /// Gets the number of managed pool entries that exceed the current demand.
    /// </summary>
    public int ManagedPoolSurplusCount { get; }

    /// <summary>
    /// Gets the number of resource generations held by a native reference.
    /// </summary>
    /// <remarks>
    /// A generation held this way is not released and is not a trim candidate, because an object outside
    /// the runtime is using the native resource it owns. A non zero value explains retained memory that
    /// the runtime would otherwise have reclaimed.
    /// </remarks>
    public int NativeReferencedGenerationCount { get; }
}
