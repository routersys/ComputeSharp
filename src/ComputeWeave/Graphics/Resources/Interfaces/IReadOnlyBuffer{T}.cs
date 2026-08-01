namespace ComputeWeave;

/// <summary>
/// An interface representing a typed readonly buffer containing raw data stored on GPU memory.
/// </summary>
/// <typeparam name="T">The type of items stored on the buffer.</typeparam>
public interface IReadOnlyBuffer<T> : IGraphicsResource
    where T : unmanaged
{
    /// <summary>
    /// Gets the length of the current buffer.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets a single <typeparamref name="T"/> value from the current readonly buffer.
    /// </summary>
    /// <param name="i">The index of the value to get.</param>
    /// <remarks>This API can only be used from a compute shader, and will always throw if used anywhere else.</remarks>
    ref readonly T this[int i] { get; }
}
