using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Mappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class HlslSourceRewriter
{
    /// <inheritdoc/>
    protected partial void TrackKnownMethodInvocation(string metadataName)
    {
        Requirements.SynchronizesTheWholeThreadGroup |= HlslKnownMethods.SynchronizesTheWholeThreadGroup(metadataName);
    }

    /// <summary>
    /// Reports an intrinsic that writes through an out parameter being given a matrix the compiler terminates on.
    /// </summary>
    /// <param name="node">The invocation that is about to be written out as it stands.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>Whether the invocation was reported, in which case the caller leaves it alone.</returns>
    /// <remarks>
    /// <para>
    /// DXC terminates with an access violation on these combinations, taking the compiler process with it, so the
    /// build fails with a native fatal error naming no source line. Reporting here keeps the call from reaching
    /// the compiler at all, the compilation step being skipped for a shader whose rewriting produced an error,
    /// which is what lets the author see their own line instead of a stack trace.
    /// </para>
    /// <para>
    /// Two shapes were measured to terminate, and the condition is written over both rather than over a name. One
    /// out parameter terminates on an integer matrix, on modf, frexp and sincos alike. Two of them terminate on a
    /// matrix of any element type, which only sincos declares today. A floating point matrix given to modf or to
    /// frexp compiles, and so does a vector or a scalar given to any of them, so none of those is touched.
    /// </para>
    /// <para>
    /// The report is on the type the two rewriters share, because both of them write intrinsic calls out. An
    /// initializer hands the compiler the same combination when the out argument is an identifier. When it is a
    /// declaration instead, the declaration is written out as it stands and refused before that point is reached.
    /// </para>
    /// </remarks>
    protected bool ReportMatrixOnIntrinsicWithOutParameter(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        int outParameters = 0;
        bool hasMatrix = false;
        bool hasIntegerMatrix = false;

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind == RefKind.Out)
            {
                outParameters++;
            }

            string typeName = parameter.Type.GetFullyQualifiedMetadataName();

            if (!HlslKnownTypes.IsMatrixType(typeName))
            {
                continue;
            }

            hasMatrix = true;
            hasIntegerMatrix |= HlslKnownTypes.IsKnownSignedIntegerType(typeName) || HlslKnownTypes.IsKnownUnsignedIntegerType(typeName);
        }

        // A call matching both is named by the integer one, the shape that needs only one out parameter
        string? given = (outParameters, hasIntegerMatrix, hasMatrix) switch
        {
            ( >= 1, true, _) => "an integer matrix",
            ( >= 2, _, true) => "a matrix",
            _ => null
        };

        if (given is null)
        {
            return false;
        }

        Diagnostics.Add(MatrixOnIntrinsicWithOutParameter, node, method.Name, given);

        return true;
    }
}