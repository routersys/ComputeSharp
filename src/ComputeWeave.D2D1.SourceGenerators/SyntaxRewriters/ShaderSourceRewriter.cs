using ComputeWeave.SourceGeneration.Mappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class ShaderSourceRewriter
{
    /// <inheritdoc/>
    private partial SyntaxNode RewriteSampledTextureAccess(IInvocationOperation operation, ExpressionSyntax expression, ArgumentSyntax arguments)
    {
        string fieldName = (operation.Instance as IFieldReferenceOperation)?.Member.Name ?? "";

        _ = HlslKnownKeywords.TryGetMappedName(fieldName, out string? mapped);

        // Transform an indexer syntax into a sampling call with the implicit static linear sampler.
        // For instance: texture.Sample(uv) will be rewritten as texture.Sample(__sampler__texture, uv).
        return
            InvocationExpression(expression)
            .AddArgumentListArguments(Argument(IdentifierName($"__sampler__{mapped ?? fieldName}")), arguments);
    }

    /// <inheritdoc/>
    partial void TrackKnownMethodInvocation(string metadataName)
    {
        Requirements.NeedsD2DRequiresScenePositionAttribute |= HlslKnownMethods.NeedsD2DRequiresScenePositionAttribute(metadataName);
    }
}