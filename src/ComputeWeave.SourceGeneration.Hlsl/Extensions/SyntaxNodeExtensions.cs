using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ComputeWeave.SourceGeneration.Extensions;

/// <inheritdoc/>
partial class SyntaxNodeExtensions
{
    /// <summary>
    /// Returns a <see cref="MethodDeclarationSyntax"/> with a block body.
    /// </summary>
    /// <param name="node">The input <see cref="MethodDeclarationSyntax"/> node.</param>
    /// <returns>A node like the one in input, but always with a block body.</returns>
    public static MethodDeclarationSyntax WithBlockBody(this MethodDeclarationSyntax node)
    {
        if (node.ExpressionBody is ArrowExpressionClauseSyntax arrow)
        {
            StatementSyntax statement = node.ReturnType switch
            {
                PredefinedTypeSyntax pts when pts.Keyword.IsKind(SyntaxKind.VoidKeyword) => ExpressionStatement(arrow.Expression),
                _ => ReturnStatement(arrow.Expression)
            };

            return node
                .WithBody(Block(statement))
                .WithExpressionBody(null)
                .WithSemicolonToken(MissingToken(SyntaxKind.SemicolonToken));
        }

        return node;
    }

    /// <summary>
    /// Returns a <see cref="MethodDeclarationSyntax"/> as a method definition.
    /// </summary>
    /// <param name="node">The input <see cref="MethodDeclarationSyntax"/> node.</param>
    /// <returns>A node like the one in input, but just as a definition.</returns>
    public static MethodDeclarationSyntax AsDefinition(this MethodDeclarationSyntax node)
    {
        if (node.ExpressionBody is not null)
        {
            return node.WithExpressionBody(null).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return node.WithBody(null).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>
    /// Returns a <see cref="MethodDeclarationSyntax"/> with no default values on its parameters.
    /// </summary>
    /// <param name="node">The input <see cref="MethodDeclarationSyntax"/> node.</param>
    /// <returns>A node like the one in input, but with no default values on its parameters.</returns>
    /// <remarks>
    /// HLSL only accepts default values on the first prototype of a function, which is the forward declaration
    /// produced by <see cref="AsDefinition(MethodDeclarationSyntax)"/>. The implementation written after it must
    /// not repeat them. Call sites are unaffected, as they bind the defaults from the forward declaration.
    /// </remarks>
    public static MethodDeclarationSyntax WithoutParameterDefaults(this MethodDeclarationSyntax node)
    {
        return node.WithParameterList(node.ParameterList.WithoutParameterDefaults());
    }

    /// <summary>
    /// Returns a <see cref="LocalFunctionStatementSyntax"/> with a block body.
    /// </summary>
    /// <param name="node">The input <see cref="LocalFunctionStatementSyntax"/> node.</param>
    /// <returns>A node like the one in input, but always with a block body.</returns>
    /// <remarks>
    /// This method is the same as <see cref="WithBlockBody(MethodDeclarationSyntax)"/>, but it is necessary to
    /// duplicate the code because the two types don't have a common base type or interface that can be leveraged.
    /// </remarks>
    public static LocalFunctionStatementSyntax WithBlockBody(this LocalFunctionStatementSyntax node)
    {
        if (node.ExpressionBody is ArrowExpressionClauseSyntax arrow)
        {
            StatementSyntax statement = node.ReturnType switch
            {
                PredefinedTypeSyntax pts when pts.Keyword.IsKind(SyntaxKind.VoidKeyword) => ExpressionStatement(arrow.Expression),
                _ => ReturnStatement(arrow.Expression)
            };

            return node
                .WithBody(Block(statement))
                .WithExpressionBody(null)
                .WithSemicolonToken(MissingToken(SyntaxKind.SemicolonToken));
        }

        return node;
    }

    /// <summary>
    /// Returns a <see cref="LocalFunctionStatementSyntax"/> as a method definition.
    /// </summary>
    /// <param name="node">The input <see cref="LocalFunctionStatementSyntax"/> node.</param>
    /// <returns>A node like the one in input, but just as a definition.</returns>
    public static LocalFunctionStatementSyntax AsDefinition(this LocalFunctionStatementSyntax node)
    {
        if (node.ExpressionBody is not null)
        {
            return node.WithExpressionBody(null).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return node.WithBody(null).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    /// <inheritdoc cref="WithoutParameterDefaults(MethodDeclarationSyntax)"/>
    /// <param name="node">The input <see cref="LocalFunctionStatementSyntax"/> node.</param>
    /// <returns>A node like the one in input, but with no default values on its parameters.</returns>
    /// <remarks>
    /// This method is the same as <see cref="WithoutParameterDefaults(MethodDeclarationSyntax)"/>, but it is
    /// necessary to duplicate the code because the two types don't have a common base type or interface that
    /// can be leveraged.
    /// </remarks>
    public static LocalFunctionStatementSyntax WithoutParameterDefaults(this LocalFunctionStatementSyntax node)
    {
        return node.WithParameterList(node.ParameterList.WithoutParameterDefaults());
    }

    /// <summary>
    /// Returns a <see cref="ConstructorDeclarationSyntax"/> with a block body.
    /// </summary>
    /// <param name="node">The input <see cref="ConstructorDeclarationSyntax"/> node.</param>
    /// <returns>A node like the one in input, but always with a block body.</returns>
    /// <remarks>
    /// This method is the counterpart of <see cref="WithBlockBody(MethodDeclarationSyntax)"/> for a constructor,
    /// which has no return type, so the expression is always turned into an expression statement.
    /// </remarks>
    public static ConstructorDeclarationSyntax WithBlockBody(this ConstructorDeclarationSyntax node)
    {
        if (node.ExpressionBody is ArrowExpressionClauseSyntax arrow)
        {
            return node
                .WithBody(Block(ExpressionStatement(arrow.Expression)))
                .WithExpressionBody(null)
                .WithSemicolonToken(MissingToken(SyntaxKind.SemicolonToken));
        }

        return node;
    }

    /// <summary>
    /// Returns a <see cref="ParameterListSyntax"/> with no default values on its parameters.
    /// </summary>
    /// <param name="node">The input <see cref="ParameterListSyntax"/> node.</param>
    /// <returns>A node like the one in input, but with no default values on its parameters.</returns>
    private static ParameterListSyntax WithoutParameterDefaults(this ParameterListSyntax node)
    {
        SeparatedSyntaxList<ParameterSyntax> parameters = node.Parameters;

        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Default is not null)
            {
                parameters = parameters.Replace(parameters[i], parameters[i].WithDefault(null));
            }
        }

        // Roslyn returns the same node when the list is unchanged, so the common case allocates nothing
        return node.WithParameters(parameters);
    }

    /// <summary>
    /// Returns a <see cref="MethodDeclarationSyntax"/> instance with no invalid HLSL modifiers.
    /// </summary>
    /// <param name="node">The input <see cref="MethodDeclarationSyntax"/> node.</param>
    /// <returns>A node just like <paramref name="node"/> but with no invalid HLSL modifiers.</returns>
    /// <remarks>
    /// The modifiers HLSL knows on a function are kept and every other one is dropped. Naming the ones to drop
    /// instead lets a modifier the list does not name reach the shader compiler, which reports it against
    /// generated code the author never wrote. The refusals for the modifiers that are their own error read the
    /// declaration the author wrote, so dropping one here hides nothing.
    /// </remarks>
    public static MethodDeclarationSyntax WithoutInvalidHlslModifiers(this MethodDeclarationSyntax node)
    {
        static bool IsAllowedHlslModifier(SyntaxToken syntaxToken)
        {
            return syntaxToken.Kind() is SyntaxKind.StaticKeyword;
        }

        return node.WithModifiers(TokenList(node.Modifiers.Where(IsAllowedHlslModifier)));
    }

    /// <summary>
    /// Returns an <see cref="ExpressionSyntax"/> instance that is safe to use as the target of a member access.
    /// </summary>
    /// <param name="node">The input <see cref="ExpressionSyntax"/> node.</param>
    /// <returns>A node just like <paramref name="node"/>, parenthesized unless it already is a primary expression.</returns>
    /// <remarks>
    /// Printing a syntax tree only emits its tokens, so an expression embedded into a context that binds more
    /// tightly than its own outermost operator would produce text that parses back into a different tree.
    /// </remarks>
    public static ExpressionSyntax AsPrimaryExpression(this ExpressionSyntax node)
    {
        return node is
            IdentifierNameSyntax or
            LiteralExpressionSyntax or
            InvocationExpressionSyntax or
            MemberAccessExpressionSyntax or
            ElementAccessExpressionSyntax or
            ParenthesizedExpressionSyntax
            ? node
            : ParenthesizedExpression(node);
    }

    /// <summary>
    /// Returns an <see cref="ExpressionSyntax"/> instance that is safe to use as the operand of a unary or binary operator.
    /// </summary>
    /// <param name="node">The input <see cref="ExpressionSyntax"/> node.</param>
    /// <returns>A node just like <paramref name="node"/>, parenthesized unless it already binds at least as tightly.</returns>
    /// <remarks><inheritdoc cref="AsPrimaryExpression(ExpressionSyntax)" path="/remarks/node()"/></remarks>
    public static ExpressionSyntax AsOperand(this ExpressionSyntax node)
    {
        return node is
            CastExpressionSyntax or
            PrefixUnaryExpressionSyntax or
            PostfixUnaryExpressionSyntax
            ? node
            : node.AsPrimaryExpression();
    }
}