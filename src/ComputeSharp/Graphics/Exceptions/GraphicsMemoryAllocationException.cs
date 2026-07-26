using System;

namespace ComputeSharp;

/// <summary>
/// A custom <see cref="InvalidOperationException"/> that indicates when a native resource allocation was not admitted.
/// </summary>
public sealed class GraphicsMemoryAllocationException : InvalidOperationException
{
    /// <summary>
    /// Creates a new <see cref="GraphicsMemoryAllocationException"/> instance.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public GraphicsMemoryAllocationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="GraphicsMemoryAllocationException"/> instance.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public GraphicsMemoryAllocationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
