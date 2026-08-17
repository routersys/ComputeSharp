namespace ComputeWeave;

/// <summary>
/// An exception carrying the identifier of the runtime diagnostic that rejected a call.
/// </summary>
/// <remarks>
/// The identifier is a stable contract. Callers switch on it to tell one rejection from another, so that they
/// can retry, rebuild the resource or tear the domain down as the rejection requires. Exception messages carry
/// no contract and change with the implementation, so they must not be used for that.
/// </remarks>
public interface IComputeDiagnostic
{
    /// <summary>
    /// Gets the identifier of the diagnostic that rejected the call.
    /// </summary>
    string DiagnosticId { get; }
}
