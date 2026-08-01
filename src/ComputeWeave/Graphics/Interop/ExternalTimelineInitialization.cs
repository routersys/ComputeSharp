namespace ComputeWeave;

/// <summary>
/// The timeline information the runtime hands to an external interop provider while initializing it.
/// </summary>
public readonly ref struct ExternalTimelineInitialization
{
    /// <summary>
    /// The borrowed shared NT handle of the fence backing the domain timeline.
    /// </summary>
    private readonly BorrowedSharedHandle sharedFenceHandle;

    /// <summary>
    /// Creates a new <see cref="ExternalTimelineInitialization"/> instance with the specified parameters.
    /// </summary>
    /// <param name="sharedFenceHandle">The borrowed shared NT handle of the fence backing the domain timeline.</param>
    internal ExternalTimelineInitialization(BorrowedSharedHandle sharedFenceHandle)
    {
        this.sharedFenceHandle = sharedFenceHandle;
    }

    /// <summary>
    /// Gets the borrowed shared NT handle of the fence backing the domain timeline.
    /// </summary>
    /// <remarks>
    /// The timeline values themselves belong to the runtime. A provider opens the fence from this
    /// handle, and never generates, changes or reuses a value on it.
    /// </remarks>
    public BorrowedSharedHandle SharedFenceHandle => this.sharedFenceHandle;
}
