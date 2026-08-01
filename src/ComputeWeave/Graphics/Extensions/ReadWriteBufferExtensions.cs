using System;

namespace ComputeWeave;

/// <summary>
/// A <see langword="class"/> that contains extension methods for the <see cref="ReadWriteBuffer{T}"/> type.
/// </summary>
public static class ReadWriteBufferExtensions
{
    /// <summary>
    /// Retrieves a wrapping <see cref="IReadOnlyBuffer{T}"/> instance for the input resource.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer.</typeparam>
    /// <param name="buffer">The input <see cref="ReadWriteBuffer{T}"/> instance to create a wrapper for.</param>
    /// <returns>An <see cref="IReadOnlyBuffer{T}"/> instance wrapping the current resource.</returns>
    /// <remarks>
    /// <para>The returned instance binds the current buffer through its SRV, so a shader taking it can only read from it.</para>
    /// <para>
    /// Unlike the texture counterparts, a buffer resides in the common state and needs no transition to be read through
    /// an SRV, so the returned instance stays valid for the whole lifetime of the buffer and can be cached and reused.
    /// The compute queue still has to observe the writes of a previous dispatch before reading them back, which the
    /// ordering between submissions on the queue already guarantees.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the current instance or its associated device are disposed.</exception>
    public static IReadOnlyBuffer<T> AsReadOnly<T>(this ReadWriteBuffer<T> buffer)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(buffer);

        return buffer.AsReadOnly();
    }
}
