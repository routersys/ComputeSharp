using ComputeWeave.SourceGeneration.Mappings;
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

    /// <inheritdoc/>
    private partial void TrackKnownMethodInvocation(string metadataName)
    {
        // Track whether the method needs [D2DRequiresScenePosition]
        Requirements.NeedsD2DRequiresScenePositionAttribute |= HlslKnownMethods.NeedsD2DRequiresScenePositionAttribute(metadataName);
    }
}