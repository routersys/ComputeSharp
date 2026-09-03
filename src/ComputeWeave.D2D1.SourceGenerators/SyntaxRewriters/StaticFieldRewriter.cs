using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class StaticFieldRewriter
{
    /// <inheritdoc/>
    private partial void TrackKnownPropertyAccess(IMemberReferenceOperation operation, MemberAccessExpressionSyntax node)
    {
        // No special tracking is needed for D2D1 shaders
    }
}