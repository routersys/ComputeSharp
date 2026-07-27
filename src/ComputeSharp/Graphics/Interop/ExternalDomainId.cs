namespace ComputeSharp;

/// <summary>
/// An identifier of a compute interop domain registered on a graphics device.
/// </summary>
public readonly struct ExternalDomainId
{
    /// <summary>
    /// The raw value of the current identifier.
    /// </summary>
    private readonly ulong value;

    /// <summary>
    /// Creates a new <see cref="ExternalDomainId"/> instance with the specified parameters.
    /// </summary>
    /// <param name="value">The raw value of the identifier.</param>
    internal ExternalDomainId(ulong value)
    {
        this.value = value;
    }

    /// <summary>
    /// Gets the raw value of the current identifier.
    /// </summary>
    /// <remarks>
    /// Domain identities are 1-based, so <c>0</c> is reserved to mean that no domain is referenced.
    /// </remarks>
    public ulong Value => this.value;
}
