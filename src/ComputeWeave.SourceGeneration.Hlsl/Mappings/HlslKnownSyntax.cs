using Microsoft.CodeAnalysis.CSharp;

namespace ComputeWeave.SourceGeneration.Mappings;

/// <summary>
/// A <see langword="class"/> that contains the set of C# syntax kinds a shader body may use.
/// </summary>
/// <remarks>
/// The set is measured, not designed. It is the union of the kinds the rewriter walks when the whole
/// solution is built, and the kinds of the constructs that were built one at a time and shown to compute
/// the same value on a device. Widening it requires the same measurement: see the shader language surface
/// specification. A kind that is not here is one the set records no verdict for, which is not the same as
/// one nothing has judged: a kind the rewriter always refuses is not here either. Where nothing else answers
/// for it, the generator must not silently write it into HLSL.
/// </remarks>
internal static class HlslKnownSyntax
{
    /// <summary>
    /// Checks whether a given syntax kind is one a shader body may use.
    /// </summary>
    /// <param name="kind">The syntax kind to check.</param>
    /// <returns>Whether the syntax kind is in the accepted set.</returns>
    public static bool IsAccepted(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.AddAssignmentExpression or SyntaxKind.AddExpression or SyntaxKind.AliasQualifiedName or
            SyntaxKind.AndAssignmentExpression or SyntaxKind.Argument or SyntaxKind.ArgumentList or
            SyntaxKind.ArrowExpressionClause or SyntaxKind.Attribute or SyntaxKind.AttributeArgument or
            SyntaxKind.AttributeArgumentList or SyntaxKind.AttributeList or SyntaxKind.AttributeTargetSpecifier or
            SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseNotExpression or SyntaxKind.BitwiseOrExpression or
            SyntaxKind.Block or SyntaxKind.BracketedArgumentList or SyntaxKind.BreakStatement or
            SyntaxKind.CaseSwitchLabel or SyntaxKind.CastExpression or SyntaxKind.CharacterLiteralExpression or
            SyntaxKind.ConditionalExpression or SyntaxKind.ConstructorDeclaration or SyntaxKind.ContinueStatement or
            SyntaxKind.DeclarationExpression or SyntaxKind.DefaultExpression or SyntaxKind.DefaultLiteralExpression or
            SyntaxKind.DefaultSwitchLabel or SyntaxKind.DivideAssignmentExpression or SyntaxKind.DivideExpression or
            SyntaxKind.DoStatement or SyntaxKind.ElementAccessExpression or SyntaxKind.ElseClause or
            SyntaxKind.EmptyStatement or SyntaxKind.EqualsExpression or SyntaxKind.EqualsValueClause or
            SyntaxKind.ExclusiveOrAssignmentExpression or SyntaxKind.ExclusiveOrExpression or SyntaxKind.ExplicitInterfaceSpecifier or
            SyntaxKind.ExpressionStatement or SyntaxKind.FalseLiteralExpression or SyntaxKind.ForStatement or
            SyntaxKind.GenericName or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression or
            SyntaxKind.IdentifierName or SyntaxKind.IfStatement or SyntaxKind.ImplicitObjectCreationExpression or
            SyntaxKind.InvocationExpression or SyntaxKind.LeftShiftAssignmentExpression or SyntaxKind.LeftShiftExpression or
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.LocalDeclarationStatement or
            SyntaxKind.LocalFunctionStatement or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalNotExpression or
            SyntaxKind.LogicalOrExpression or SyntaxKind.MethodDeclaration or SyntaxKind.ModuloAssignmentExpression or
            SyntaxKind.ModuloExpression or SyntaxKind.MultiplyAssignmentExpression or SyntaxKind.MultiplyExpression or
            SyntaxKind.NotEqualsExpression or SyntaxKind.NumericLiteralExpression or SyntaxKind.ObjectCreationExpression or
            SyntaxKind.OrAssignmentExpression or SyntaxKind.Parameter or SyntaxKind.ParameterList or
            SyntaxKind.ParenthesizedExpression or SyntaxKind.PostDecrementExpression or SyntaxKind.PostIncrementExpression or
            SyntaxKind.PreDecrementExpression or SyntaxKind.PreIncrementExpression or SyntaxKind.PredefinedType or
            SyntaxKind.QualifiedName or SyntaxKind.ReturnStatement or SyntaxKind.RightShiftAssignmentExpression or
            SyntaxKind.RightShiftExpression or SyntaxKind.SimpleAssignmentExpression or SyntaxKind.SimpleMemberAccessExpression or
            SyntaxKind.SingleVariableDesignation or SyntaxKind.SubtractAssignmentExpression or SyntaxKind.SubtractExpression or
            SyntaxKind.SwitchSection or SyntaxKind.SwitchStatement or SyntaxKind.ThisExpression or
            SyntaxKind.TrueLiteralExpression or SyntaxKind.TypeArgumentList or SyntaxKind.UnaryMinusExpression or
            SyntaxKind.UnaryPlusExpression or SyntaxKind.UnsignedRightShiftAssignmentExpression or SyntaxKind.UnsignedRightShiftExpression or
            SyntaxKind.VariableDeclaration or SyntaxKind.VariableDeclarator or SyntaxKind.WhileStatement => true,
            _ => false
        };
    }
}
