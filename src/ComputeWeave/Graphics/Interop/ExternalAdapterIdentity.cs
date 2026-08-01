namespace ComputeWeave;

/// <summary>
/// The identity of the graphics adapter an external interop provider runs on.
/// </summary>
/// <param name="adapterLuid">The bit pattern of the adapter locally unique identifier.</param>
/// <remarks>
/// The bit pattern is <c>((long)HighPart &lt;&lt; 32) | LowPart</c> of the native adapter identifier.
/// </remarks>
public readonly struct ExternalAdapterIdentity(long adapterLuid)
{
    /// <summary>
    /// Gets the bit pattern of the adapter locally unique identifier.
    /// </summary>
    public long AdapterLuid { get; } = adapterLuid;
}
