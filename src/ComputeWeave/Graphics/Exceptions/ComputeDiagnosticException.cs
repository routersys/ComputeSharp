using System;

namespace ComputeWeave;

/// <summary>
/// The exception thrown when a runtime diagnostic rejects a call.
/// </summary>
/// <remarks>
/// This derives from <see cref="InvalidOperationException"/> so that callers already catching that keep
/// catching these rejections. Use <see cref="DiagnosticId"/> to tell one rejection from another.
/// </remarks>
public class ComputeDiagnosticException : InvalidOperationException, IComputeDiagnostic
{
    /// <summary>
    /// Creates a new <see cref="ComputeDiagnosticException"/> instance with the specified parameters.
    /// </summary>
    /// <param name="diagnosticId">The identifier of the diagnostic that rejected the call.</param>
    /// <param name="message">The message describing the rejection.</param>
    public ComputeDiagnosticException(string diagnosticId, string message)
        : base(message)
    {
        DiagnosticId = diagnosticId;
    }

    /// <summary>
    /// Creates a new <see cref="ComputeDiagnosticException"/> instance with the specified parameters.
    /// </summary>
    /// <param name="diagnosticId">The identifier of the diagnostic that rejected the call.</param>
    /// <param name="message">The message describing the rejection.</param>
    /// <param name="innerException">The exception that caused the rejection, if any.</param>
    public ComputeDiagnosticException(string diagnosticId, string message, Exception? innerException)
        : base(message, innerException)
    {
        DiagnosticId = diagnosticId;
    }

    /// <inheritdoc/>
    public string DiagnosticId { get; }
}
