using ComputeWeave.SourceGeneration.Mappings;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class HlslSourceRewriter
{
    /// <inheritdoc/>
    protected partial void TrackKnownMethodInvocation(string metadataName)
    {
        Requirements.NeedsD2DRequiresScenePositionAttribute |= HlslKnownMethods.NeedsD2DRequiresScenePositionAttribute(metadataName);
    }
}