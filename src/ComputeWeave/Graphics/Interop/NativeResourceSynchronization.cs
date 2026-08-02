namespace ComputeWeave.Interop;

/// <summary>
/// The completion points of the work the runtime had already submitted for a resource generation
/// when a <see cref="NativeResourceReference"/> was acquired for it.
/// </summary>
/// <remarks>
/// The runtime does not order the work issued by the holder of a native reference against its own.
/// These completion points are the material a holder uses to establish that order itself, by enqueueing
/// a wait on the fence obtained from <see cref="InteropServices.GetID3D12Fence"/> for the reported queue.
/// </remarks>
public readonly struct NativeResourceSynchronization
{
    /// <summary>
    /// Creates a new <see cref="NativeResourceSynchronization"/> instance with the specified parameters.
    /// </summary>
    /// <param name="lastWrite">The completion of the last submission that wrote to the generation.</param>
    /// <param name="lastComputeRead">The completion of the last compute queue submission that read the generation.</param>
    /// <param name="lastCopyRead">The completion of the last copy queue submission that read the generation.</param>
    internal NativeResourceSynchronization(FencePoint lastWrite, FencePoint lastComputeRead, FencePoint lastCopyRead)
    {
        LastWrite = lastWrite;
        LastComputeRead = lastComputeRead;
        LastCopyRead = lastCopyRead;
    }

    /// <summary>
    /// Gets the completion of the last submission that wrote to the generation.
    /// </summary>
    public FencePoint LastWrite { get; }

    /// <summary>
    /// Gets the completion of the last compute queue submission that read the generation.
    /// </summary>
    public FencePoint LastComputeRead { get; }

    /// <summary>
    /// Gets the completion of the last copy queue submission that read the generation.
    /// </summary>
    public FencePoint LastCopyRead { get; }
}
