namespace ComputeSharp;

/// <summary>
/// The memory statistics observed for a single memory segment of a graphics device.
/// </summary>
public readonly struct GraphicsMemorySegmentStatistics
{
    /// <summary>
    /// Creates a new <see cref="GraphicsMemorySegmentStatistics"/> instance with the specified parameters.
    /// </summary>
    /// <param name="segment">The memory segment the statistics describe.</param>
    /// <param name="status">The status of the observed video memory budget.</param>
    /// <param name="budgetBytes">The number of bytes the process is budgeted for.</param>
    /// <param name="currentProcessUsageBytes">The number of bytes the process currently uses.</param>
    /// <param name="computeSharpOwnedBytes">The number of bytes owned by native resources the device created.</param>
    /// <param name="reservationBytes">The number of bytes reserved by allocations that have not been committed.</param>
    /// <param name="retiredPendingBytes">The number of bytes owned by retired generations that have not been released.</param>
    internal GraphicsMemorySegmentStatistics(
        GraphicsMemorySegment segment,
        MemoryBudgetStatus status,
        ulong budgetBytes,
        ulong currentProcessUsageBytes,
        ulong computeSharpOwnedBytes,
        ulong reservationBytes,
        ulong retiredPendingBytes)
    {
        Segment = segment;
        Status = status;
        BudgetBytes = budgetBytes;
        CurrentProcessUsageBytes = currentProcessUsageBytes;
        ComputeSharpOwnedBytes = computeSharpOwnedBytes;
        ReservationBytes = reservationBytes;
        RetiredPendingBytes = retiredPendingBytes;
    }

    /// <summary>
    /// Gets the memory segment the statistics describe.
    /// </summary>
    public GraphicsMemorySegment Segment { get; }

    /// <summary>
    /// Gets the status of the observed video memory budget.
    /// </summary>
    public MemoryBudgetStatus Status { get; }

    /// <summary>
    /// Gets the number of bytes the process is budgeted for.
    /// </summary>
    public ulong BudgetBytes { get; }

    /// <summary>
    /// Gets the number of bytes the process currently uses.
    /// </summary>
    public ulong CurrentProcessUsageBytes { get; }

    /// <summary>
    /// Gets the number of bytes owned by native resources the device created.
    /// </summary>
    public ulong ComputeSharpOwnedBytes { get; }

    /// <summary>
    /// Gets the number of bytes reserved by allocations that have not been committed.
    /// </summary>
    public ulong ReservationBytes { get; }

    /// <summary>
    /// Gets the number of bytes owned by retired generations that have not been released.
    /// </summary>
    public ulong RetiredPendingBytes { get; }
}
