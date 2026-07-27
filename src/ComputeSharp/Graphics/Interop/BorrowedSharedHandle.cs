namespace ComputeSharp;

/// <summary>
/// A borrow of a shared NT handle the runtime owns for the duration of a single provider call.
/// </summary>
/// <remarks>
/// The runtime is the only owner of the borrowed handle. A provider must not store, duplicate
/// or close it, and must not use it after the call it was passed to has returned.
/// </remarks>
public readonly ref struct BorrowedSharedHandle
{
    /// <summary>
    /// The borrowed shared NT handle.
    /// </summary>
    private readonly nint handle;

    /// <summary>
    /// Creates a new <see cref="BorrowedSharedHandle"/> instance with the specified parameters.
    /// </summary>
    /// <param name="handle">The shared NT handle to borrow.</param>
    internal BorrowedSharedHandle(nint handle)
    {
        this.handle = handle;
    }

    /// <summary>
    /// Gets the borrowed shared NT handle.
    /// </summary>
    /// <returns>The borrowed shared NT handle.</returns>
    public nint DangerousGetHandle()
    {
        return this.handle;
    }
}
