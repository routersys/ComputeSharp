using System;

namespace ComputeSharp;

/// <summary>
/// A registered client that reports the memory grants issued to a single graphics device.
/// </summary>
public interface IGraphicsMemoryBudgetClient : IDisposable
{
    /// <summary>
    /// Tries to get the current grant for a given memory segment.
    /// </summary>
    /// <param name="segment">The memory segment to get the grant of.</param>
    /// <param name="grant">The resulting grant, if one is available.</param>
    /// <returns>Whether a grant is available for <paramref name="segment"/>.</returns>
    /// <remarks>Implementations must be thread safe and must not call back into the runtime.</remarks>
    bool TryGetGrant(GraphicsMemorySegment segment, out GraphicsMemoryGrant grant);
}
