using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Mappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc cref="HlslSourceRewriter"/>
partial class HlslSourceRewriter
{
    /// <summary>
    /// Reports a syntax kind that is outside the set a shader body may use.
    /// </summary>
    /// <param name="node">The node that is about to be written out with nothing recorded about its kind.</param>
    /// <remarks>
    /// <para>
    /// This is called from <see cref="Visit"/>, which every node the rewriting reaches passes through, so what
    /// is seen here does not depend on which visit methods happen to exist. The declaration a rewriting starts
    /// from is the one exception: the typed overloads take a method or a constructor to the base method
    /// directly, and the one for a variable declarator forwards its initializer. Those three kinds are in the
    /// set, so nothing is passed over that the set does not already answer for.
    /// </para>
    /// <para>
    /// The set is measured rather than designed, so a kind outside it is one the set records no verdict for.
    /// That is not the same as one nothing has judged: a kind a visit method always refuses is outside the set
    /// as well, because refusing it keeps it out of what the measurement sees, and such a kind is answered by
    /// that refusal, this report being dropped beside it. Nodes are what reaches here, so a modifier or any
    /// other token keeps whatever diagnostic it already has.
    /// </para>
    /// <para>
    /// A kind is reported once per rewriter, which is once per method, so a construct used many times gives
    /// one report rather than one per use.
    /// </para>
    /// <para>
    /// Nothing under an attribute list reaches here, the list being returned without being walked, so a kind
    /// written only inside one is neither reported nor recorded as seen.
    /// </para>
    /// </remarks>
    protected void ReportSyntaxOutsideTheAcceptedSet(SyntaxNode node)
    {
        SyntaxKind kind = node.Kind();

        if (!HlslKnownSyntax.IsAccepted(kind) &&
            this.reportedSyntaxKinds.Add(kind))
        {
            Diagnostics.Add(UnknownShaderSyntax, node, kind.ToString());
        }
    }

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
    /// Reports an element access that every mapping has declined, when HLSL has no indexer for the target.
    /// </summary>
    /// <param name="node">The element access that is about to be written out as it stands.</param>
    /// <remarks>
    /// <para>
    /// The indexers HLSL provides are the ones on its vector and matrix types, the ones on the resource
    /// types, and the one on an array. An indexer declared anywhere else is not imported, so the accessor
    /// the author wrote never runs, and the access reaches the HLSL compiler naming a type it never saw.
    /// </para>
    /// <para>
    /// What is asked is where the indexer is declared, and not just what is being indexed. An extension
    /// indexer over a type HLSL can index resolves to an accessor of the author's while the access is
    /// written out as the built-in one, which compiles and computes a different value.
    /// </para>
    /// <para>
    /// The report names the type being indexed rather than the indexer, because an inline array has no
    /// indexer of its own: the access resolves through a span that the author never wrote.
    /// </para>
    /// </remarks>
    protected void ReportUnmappedElementAccess(ElementAccessExpressionSyntax node)
    {
        ITypeSymbol? type = SemanticModel.For(node).GetTypeInfo(node.Expression, CancellationToken).Type;

        // A group shared array is declared in HLSL as an array, so its element access needs no mapping
        if (type is null or IArrayTypeSymbol)
        {
            return;
        }

        if (SemanticModel.For(node).GetOperation(node, CancellationToken) is IPropertyReferenceOperation operation &&
            HlslKnownTypes.IsKnownIndexableType(operation.Property.ContainingType.GetFullyQualifiedMetadataName()))
        {
            return;
        }

        Diagnostics.Add(InvalidElementAccess, node, type);
    }

    /// <summary>
    /// Reports an invocation of a generic method, which HLSL has no way to express.
    /// </summary>
    /// <param name="node">The invocation that is about to be written out as it stands.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>Whether the invocation was reported, in which case the caller leaves it alone.</returns>
    /// <remarks>
    /// HLSL has no type parameters. A mapped intrinsic is written out under its HLSL name, which drops the
    /// type arguments and stays correct, so a mapping is asked for first. Every other target is either
    /// imported by rewriting its declaration, which carries the type parameter list into the generated
    /// source, or written out as it stands. The resource samplers are matched by a separate table that
    /// spells out concrete parameter types, so no generic method reaches it.
    /// </remarks>
    protected bool ReportUnmappedGenericMethodCall(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        // A local function is answered for at its declaration, so reporting the call as well would name two
        // places for one cause
        if (!method.IsGenericMethod ||
            method.MethodKind == MethodKind.LocalFunction ||
            HlslKnownMethods.TryGetMappedName(method.GetFullyQualifiedMetadataName(), out _, out _))
        {
            return false;
        }

        Diagnostics.Add(InvalidGenericMethodCall, node, method);

        return true;
    }

    /// <summary>
    /// Reports an invocation of a member declared in a C# extension block, which is never imported.
    /// </summary>
    /// <param name="node">The invocation that is about to be written out as it stands.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>Whether the invocation was reported, in which case the caller leaves it alone.</returns>
    /// <remarks>
    /// <para>
    /// An extension block declares its members on a type of its own, which the import path never reaches:
    /// that path takes a static method, or an instance method on a struct, and an extension member is an
    /// instance member of neither. The body the author wrote therefore never runs, and the call is written
    /// out as it stands, naming a member the HLSL compiler never saw.
    /// </para>
    /// <para>
    /// The type is asked whether it can be referenced by name rather than for its kind. The kind for an
    /// extension declaration was added to Roslyn after the version these generators compile against, so it
    /// cannot be named here, whereas a type the author cannot write is what an extension declaration is.
    /// </para>
    /// <para>
    /// An extension method declared with a <see langword="this"/> parameter is unaffected, its target being
    /// a static method on the enclosing class. So is a static method declared inside an extension block,
    /// which belongs to that same enclosing class and is imported through the static path.
    /// </para>
    /// </remarks>
    protected bool ReportUnmappedExtensionMemberCall(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        if (method.ContainingType.CanBeReferencedByName)
        {
            return false;
        }

        Diagnostics.Add(InvalidExtensionMemberCall, node, method);

        return true;
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
    /// An operator HLSL does provide is still refused when C# widens its operands to a type outside the HLSL
    /// type set, which a signed and an unsigned integer in one operation do. That widening cannot be written
    /// into the generated code, so the operands reach the shader compiler as they stand and the operation is
    /// resolved over them instead, where the unsigned kind wins: a comparison answers the other way and an
    /// arithmetic result wraps at 32 bits, with neither compiler reporting anything.
    /// </para>
    /// <para>
    /// The walk is over the resolved operations rather than the syntax, because an implicit conversion has no
    /// node of its own. Reporting from a visit method would reach every other form and miss that one, and the
    /// same widening is reached by a binary operator and by a unary minus on an unsigned value. A conditional
    /// with one arm of each kind has no natural type at all, so it is target typed and never widens.
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
            else if (GetWidenedOperandType(operation) is ITypeSymbol widenedType && IsInnermostWidening(operation))
            {
                Diagnostics.Add(InvalidOperandWidening, operation.Syntax, widenedType);
            }
        }

        // Reads the type C# widened the operands of an operation to, when that type is outside the HLSL type
        // set and the operand it was reached from is inside it. A comparison answers with a bool, so what is
        // read is an operand and not the result; both operands are brought to the same type, so one is enough
        static ITypeSymbol? GetWidenedOperandType(IOperation operation)
        {
            IOperation? operand = operation switch
            {
                IBinaryOperation binaryOperation => binaryOperation.LeftOperand,
                IUnaryOperation unaryOperation => unaryOperation.Operand,
                _ => null
            };

            if (operand is not IConversionOperation { Operand.Type: { } source, Type: { } target } ||
                !HlslKnownTypes.IsKnownHlslType(source.GetFullyQualifiedMetadataName()) ||
                HlslKnownTypes.IsKnownHlslType(target.GetFullyQualifiedMetadataName()))
            {
                return null;
            }

            return target;
        }

        // An operation holding another widened one is widening a result that is already outside the set, so
        // it names a consequence rather than the place the author has to change
        static bool IsInnermostWidening(IOperation operation)
        {
            foreach (IOperation descendant in operation.Descendants())
            {
                if (GetWidenedOperandType(descendant) is not null)
                {
                    return false;
                }
            }

            return true;
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