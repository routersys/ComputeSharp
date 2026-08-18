namespace ComputeWeave.Resources.Lifetime;

/// <summary>
/// The outcome of trying to acquire a persistent lease over the external view of a published generation.
/// </summary>
/// <remarks>
/// The conditions are separated so that the caller can reject each one with the runtime diagnostic that
/// matches it, instead of collapsing them into a single message.
/// </remarks>
internal enum PersistentLeaseStatus
{
    /// <summary>The lease was acquired.</summary>
    Acquired = 0,

    /// <summary>The slot has no published generation the external queue could lease.</summary>
    GenerationUnavailable = 1,

    /// <summary>The interop domain no longer accepts a persistent lease reference.</summary>
    DomainUnavailable = 2,

    /// <summary>The resource set registration no longer accepts a persistent lease.</summary>
    RegistrationUnavailable = 3
}
