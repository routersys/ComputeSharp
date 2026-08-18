using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ComputeWeave;

namespace System;

/// <summary>
/// Throw helper extensions for <see cref="ComputeDiagnosticException"/>.
/// </summary>
internal static class ComputeDiagnosticExceptionExtensions
{
    /// <summary>
    /// Throws a <see cref="ComputeDiagnosticException"/> if <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="_">Dummy value to invoke the extension upon (always pass <see langword="null"/>.</param>
    /// <param name="condition">The condition to decide whether to throw the exception.</param>
    /// <param name="diagnosticId">The identifier of the diagnostic that rejects the call.</param>
    /// <param name="message">The message to include in the exception.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf(this ComputeDiagnosticException? _, [DoesNotReturnIf(true)] bool condition, string diagnosticId, string message)
    {
        if (condition)
        {
            Throw(diagnosticId, message);
        }
    }

    /// <summary>
    /// Throws a <see cref="ComputeDiagnosticException"/> with a given diagnostic identifier and message.
    /// </summary>
    /// <param name="diagnosticId">The identifier of the diagnostic that rejects the call.</param>
    /// <param name="message">The message to include in the exception.</param>
    /// <exception cref="ComputeDiagnosticException">Always thrown.</exception>
    [DoesNotReturn]
    private static void Throw(string diagnosticId, string message)
    {
        throw new ComputeDiagnosticException(diagnosticId, message);
    }
}
