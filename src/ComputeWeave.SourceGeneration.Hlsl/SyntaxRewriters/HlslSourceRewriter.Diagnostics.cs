using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Mappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc cref="HlslSourceRewriter"/>
partial class HlslSourceRewriter
{
    /// <summary>
    /// Reports a member access that every mapping has declined, when HLSL cannot express it.
    /// </summary>
    /// <param name="node">The member access that is about to be written out as it stands.</param>
    /// <param name="operation">The resolved member reference for <paramref name="node"/>.</param>
    /// <remarks>
    /// Only a property is reported. A field is written out as it stands on purpose, because HLSL structs
    /// carry fields, whereas a property is left out of the generated struct entirely. Without this the
    /// access reaches the HLSL compiler, which names generated code the author never wrote.
    /// </remarks>
    protected void ReportUnmappedMemberAccess(MemberAccessExpressionSyntax node, IMemberReferenceOperation operation)
    {
        if (operation is IPropertyReferenceOperation)
        {
            Diagnostics.Add(InvalidPropertyAccess, node, operation.Member);
        }
    }

    /// <summary>
    /// Reports every operator a rewritten declaration resolves that HLSL cannot express.
    /// </summary>
    /// <param name="declaration">The declaration whose body has just been rewritten.</param>
    /// <remarks>
    /// <para>
    /// The operators declared on the HLSL primitive types are either mapped to an intrinsic or left as they
    /// stand, both of which are correct. An operator declared anywhere else is not imported, so the body the
    /// author wrote never runs. Most forms then fail in the HLSL compiler, but a conversion between a struct
    /// and a scalar is one HLSL performs on its own, taking the first member or filling every member, and the
    /// shader silently computes a different value.
    /// </para>
    /// <para>
    /// The walk is over the resolved operations rather than the syntax, because an implicit conversion has no
    /// node of its own. Reporting from a visit method would reach every other form and miss that one.
    /// </para>
    /// </remarks>
    protected void ReportUnmappedOperators(SyntaxNode declaration)
    {
        if (SemanticModel.For(declaration).GetOperation(declaration, CancellationToken) is not IOperation body)
        {
            return;
        }

        foreach (IOperation operation in body.Descendants())
        {
            IMethodSymbol? operatorMethod = operation switch
            {
                IBinaryOperation binaryOperation => binaryOperation.OperatorMethod,
                ICompoundAssignmentOperation compoundAssignmentOperation => compoundAssignmentOperation.OperatorMethod,
                IUnaryOperation unaryOperation => unaryOperation.OperatorMethod,
                IIncrementOrDecrementOperation incrementOrDecrementOperation => incrementOrDecrementOperation.OperatorMethod,
                IConversionOperation conversionOperation => conversionOperation.OperatorMethod,
                _ => null
            };

            // An operator is only resolved when it is user defined, so a null one is a built-in operation
            if (operatorMethod is not null &&
                !HlslKnownTypes.IsKnownHlslType(operatorMethod.ContainingType.GetFullyQualifiedMetadataName()))
            {
                Diagnostics.Add(InvalidOperatorUse, operation.Syntax, operatorMethod);
            }
        }
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
    {
        Diagnostics.Add(AnonymousObjectCreationExpression, node);

        return base.VisitAnonymousObjectCreationExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
    {
        Diagnostics.Add(InitializerExpression, node);

        return base.VisitInitializerExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitCollectionExpression(CollectionExpressionSyntax node)
    {
        Diagnostics.Add(CollectionExpression, node);

        return base.VisitCollectionExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitAwaitExpression(AwaitExpressionSyntax node)
    {
        Diagnostics.Add(AwaitExpression, node);

        return base.VisitAwaitExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitCheckedExpression(CheckedExpressionSyntax node)
    {
        Diagnostics.Add(CheckedExpression, node);

        return base.VisitCheckedExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitCheckedStatement(CheckedStatementSyntax node)
    {
        Diagnostics.Add(CheckedStatement, node);

        return base.VisitCheckedStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node)
    {
        Diagnostics.Add(FixedStatement, node);

        return base.VisitFixedStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        Diagnostics.Add(ForEachStatement, node);

        return base.VisitForEachStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        Diagnostics.Add(ForEachStatement, node);

        return base.VisitForEachVariableStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitLockStatement(LockStatementSyntax node)
    {
        Diagnostics.Add(LockStatement, node);

        return base.VisitLockStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitQueryExpression(QueryExpressionSyntax node)
    {
        Diagnostics.Add(QueryExpression, node);

        return base.VisitQueryExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitRangeExpression(RangeExpressionSyntax node)
    {
        Diagnostics.Add(RangeExpression, node);

        return base.VisitRangeExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitRecursivePattern(RecursivePatternSyntax node)
    {
        Diagnostics.Add(RecursivePattern, node);

        return base.VisitRecursivePattern(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitRefType(RefTypeSyntax node)
    {
        Diagnostics.Add(RefType, node);

        return base.VisitRefType(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitRelationalPattern(RelationalPatternSyntax node)
    {
        Diagnostics.Add(RelationalPattern, node);

        return base.VisitRelationalPattern(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitSizeOfExpression(SizeOfExpressionSyntax node)
    {
        Diagnostics.Add(SizeOfExpression, node);

        return base.VisitSizeOfExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
    {
        Diagnostics.Add(StackAllocArrayCreationExpression, node);

        return base.VisitStackAllocArrayCreationExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitThrowExpression(ThrowExpressionSyntax node)
    {
        Diagnostics.Add(ThrowExpressionOrStatement, node);

        return base.VisitThrowExpression(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitThrowStatement(ThrowStatementSyntax node)
    {
        Diagnostics.Add(ThrowExpressionOrStatement, node);

        return base.VisitThrowStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitTryStatement(TryStatementSyntax node)
    {
        Diagnostics.Add(TryStatement, node);

        return base.VisitTryStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitTupleType(TupleTypeSyntax node)
    {
        Diagnostics.Add(TupleType, node);

        return base.VisitTupleType(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        Diagnostics.Add(UsingStatementOrDeclaration, node);

        return base.VisitUsingStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitYieldStatement(YieldStatementSyntax node)
    {
        Diagnostics.Add(YieldStatement, node);

        return base.VisitYieldStatement(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitFunctionPointerType(FunctionPointerTypeSyntax node)
    {
        Diagnostics.Add(FunctionPointer, node);

        return base.VisitFunctionPointerType(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitPointerType(PointerTypeSyntax node)
    {
        Diagnostics.Add(PointerType, node);

        return base.VisitPointerType(node);
    }

    /// <inheritdoc/>
    public sealed override SyntaxNode? VisitUnsafeStatement(UnsafeStatementSyntax node)
    {
        Diagnostics.Add(UnsafeStatement, node);

        return base.VisitUnsafeStatement(node);
    }

    /// <inheritdoc/>
    public override SyntaxNode? VisitThisExpression(ThisExpressionSyntax node)
    {
        // Emit a diagnostic on 'this' expressions, but only if they're not part of a member access.
        // That is, expressions such as 'this.field' are rewritten correctly to omit the 'this', so
        // so they are still allowed. But actual 'this' expressions that copy or return the entire
        // self instance are disallowed, as that use is not valid in HLSL syntax, unfortunately.
        if (node.Parent is not MemberAccessExpressionSyntax)
        {
            Diagnostics.Add(ThisExpression, node);
        }

        return base.VisitThisExpression(node);
    }
}