using System;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class HlslSourceRewriter
{
    /// <summary>
    /// Reports an intrinsic that writes through an out parameter being given an integer matrix.
    /// </summary>
    /// <param name="node">The invocation that is about to be written out as it stands.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>Whether the invocation was reported, in which case the caller leaves it alone.</returns>
    /// <remarks>
    /// <para>
    /// DXC terminates with an access violation on this combination, taking the compiler process with it, so the
    /// build fails with a native fatal error naming no source line. Reporting here keeps the call from reaching
    /// the compiler at all, the compilation step being skipped for a shader whose rewriting produced an error,
    /// which is what lets the author see their own line instead of a stack trace.
    /// </para>
    /// <para>
    /// The condition is the shape of the signature rather than a name, because modf, frexp and sincos were each
    /// measured to terminate the same way. Only Modf declares integer overloads today, so it is the only one
    /// that can be reached, but an integer overload added to either of the others is refused without this having
    /// to be revisited. A floating point matrix compiles, and so does an integer vector, so neither is touched.
    /// </para>
    /// <para>
    /// The report is on the type the two rewriters share, because both of them write intrinsic calls out. An
    /// initializer hands the compiler the same combination when the out argument is an identifier. When it is a
    /// declaration instead, the declaration is written out as it stands and refused before that point is reached.
    /// </para>
    /// </remarks>
    protected bool ReportIntegerMatrixOnIntrinsicWithOutParameter(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        bool hasOutParameter = false;
        bool hasIntegerMatrix = false;

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            hasOutParameter |= parameter.RefKind == RefKind.Out;
            hasIntegerMatrix |= IsIntegerMatrixType(parameter.Type);
        }

        if (!hasOutParameter || !hasIntegerMatrix)
        {
            return false;
        }

        Diagnostics.Add(IntegerMatrixOnIntrinsicWithOutParameter, node, method.Name);

        return true;

        // The caller has already matched a mapped intrinsic, whose parameters are the shader
        // primitives, so the name alone tells an integer matrix from anything else
        static bool IsIntegerMatrixType(ITypeSymbol type)
        {
            string name = type.Name;

            int digits = name.StartsWith("Int", StringComparison.Ordinal) ? 3
                : name.StartsWith("UInt", StringComparison.Ordinal) ? 4
                : 0;

            return digits > 0
                && name.Length == digits + 3
                && name[digits] is >= '1' and <= '4'
                && name[digits + 1] == 'x'
                && name[digits + 2] is >= '1' and <= '4';
        }
    }
}