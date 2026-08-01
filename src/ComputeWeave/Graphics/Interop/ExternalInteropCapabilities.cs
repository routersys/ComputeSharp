using System;

namespace ComputeWeave;

/// <summary>
/// The capabilities an external interop provider declares to the runtime.
/// </summary>
[Flags]
public enum ExternalInteropCapabilities : uint
{
    /// <summary>
    /// No capability is available.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider can open the shared fence backing the domain timeline.
    /// </summary>
    SharedFence = 1u << 0,

    /// <summary>
    /// The provider can open shared 2D textures and create external views over them.
    /// </summary>
    SharedTexture2D = 1u << 1,

    /// <summary>
    /// The provider enqueues every shared resource operation onto a single immediate context order.
    /// </summary>
    SingleImmediateContextOrdering = 1u << 2,

    /// <summary>
    /// The provider keeps that ordering for external views held across calls by a persistent lease.
    /// </summary>
    PersistentExternalViewOrdering = 1u << 3
}
