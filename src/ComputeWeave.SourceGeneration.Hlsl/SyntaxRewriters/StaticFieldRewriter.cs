using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGeneration.Mappings;
using ComputeWeave.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

#pragma warning disable IDE0051

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <summary>
/// A custom <see cref="CSharpSyntaxRewriter"/> type that processes C# static field to convert to HLSL static fields (possibly constant).
/// </summary>
/// <param name="shaderType">The type symbol for the shader type.</param>
/// <param name="semanticModel">The <see cref="SemanticModelProvider"/> instance for the target syntax tree.</param>
/// <param name="discoveredTypes">The set of discovered custom types.</param>
/// <param name="staticMethods">The collection of discovered static methods.</param>
/// <param name="instanceMethods">The collection of discovered instance methods for custom struct types.</param>
/// <param name="constructors">The collection of discovered constructors for custom struct types.</param>
/// <param name="constantDefinitions">The collection of discovered constant definitions.</param>
/// <param name="staticFieldDefinitions">The collection of discovered static field definitions.</param>
/// <param name="requirements">The requirements gathered for the shader being rewritten.</param>
/// <param name="diagnostics">The collection of produced <see cref="DiagnosticInfo"/> instances.</param>
/// <param name="token">The <see cref="CancellationToken"/> value for the current operation.</param>
internal sealed partial class StaticFieldRewriter(
    INamedTypeSymbol shaderType,
    SemanticModelProvider semanticModel,
    ICollection<INamedTypeSymbol> discoveredTypes,
    IDictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods,
    IDictionary<IMethodSymbol, MethodDeclarationSyntax> instanceMethods,
    IDictionary<IMethodSymbol, (MethodDeclarationSyntax, MethodDeclarationSyntax)> constructors,
    IDictionary<IFieldSymbol, string> constantDefinitions,
    IDictionary<IFieldSymbol, HlslStaticField> staticFieldDefinitions,
    HlslShaderRequirements requirements,
    ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
    CancellationToken token)
    : HlslSourceRewriter(semanticModel, discoveredTypes, constantDefinitions, staticFieldDefinitions, requirements, diagnostics, token)
{
    /// <summary>
    /// The type symbol for the shader type.
    /// </summary>
    private readonly INamedTypeSymbol shaderType = shaderType;

    /// <summary>
    /// The collection of discovered static methods.
    /// </summary>
    private readonly IDictionary<IMethodSymbol, MethodDeclarationSyntax> staticMethods = staticMethods;

    /// <summary>
    /// The local functions produced while importing methods into an initializer.
    /// </summary>
    /// <remarks>
    /// An imported method may declare one, and HLSL has no nested functions, so the rewriter that imports
    /// it lifts them to top level. They are carried out to the caller here to be written like any other.
    /// </remarks>
    private readonly Dictionary<IMethodSymbol, LocalFunctionStatementSyntax> localFunctions = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Gets the collection of local functions lifted out of the methods imported into an initializer.
    /// </summary>
    public IReadOnlyDictionary<IMethodSymbol, LocalFunctionStatementSyntax> LocalFunctions => this.localFunctions;

    /// <inheritdoc cref="CSharpSyntaxRewriter.Visit(SyntaxNode?)"/>
    public ExpressionSyntax? Visit(VariableDeclaratorSyntax? node)
    {
        if (node?.Initializer is EqualsValueClauseSyntax fieldInitializer)
        {
            // The operation of a field lives on the initializer, not on the declarator that holds it
            ReportUnmappedOperators(fieldInitializer);

            return ((EqualsValueClauseSyntax)Visit(fieldInitializer))!.Value;
        }

        return null;
    }

    /// <inheritdoc/>
    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        CancellationToken.ThrowIfCancellationRequested();

        MemberAccessExpressionSyntax updatedNode = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

        if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
            SemanticModel.For(node).GetOperation(node, CancellationToken) is IMemberReferenceOperation operation)
        {
            // Track and replace constants
            if (operation is IFieldReferenceOperation fieldOperation &&
                fieldOperation.Field.IsConst &&
                fieldOperation.Type!.TypeKind != TypeKind.Enum)
            {
                if (HlslKnownFields.TryGetMappedName(fieldOperation.Member.ToDisplayString(), out string? constantLiteral))
                {
                    return ParseMappedExpression(constantLiteral!);
                }

                if (TryGetConstantLiteral(fieldOperation.Field.ConstantValue, out string? constantValue))
                {
                    ConstantDefinitions[fieldOperation.Field] = constantValue!;

                    string ownerTypeName = ((INamedTypeSymbol)fieldOperation.Field.ContainingSymbol).ToDisplayString().ToHlslIdentifierName();
                    string constantName = $"__{ownerTypeName}__{fieldOperation.Field.Name}";

                    return IdentifierName(constantName);
                }
            }

            if (HlslKnownProperties.TryGetMappedName(operation.Member.ToDisplayString(), out string? mapping))
            {
                // Allow specialized types to track the member access, if needed
                TrackKnownPropertyAccess(operation, node);

                // Rewrite static and instance mapped members
                return operation.Member.IsStatic switch
                {
                    true => ParseMappedExpression(mapping!),
                    false => updatedNode.WithName(IdentifierName(mapping!))
                };
            }

            ReportUnmappedMemberAccess(node, operation);
        }

        return updatedNode;
    }

    /// <inheritdoc/>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        InvocationExpressionSyntax updatedNode = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        if (SemanticModel.For(node).GetOperation(node, CancellationToken) is IInvocationOperation { TargetMethod: IMethodSymbol method })
        {
            if (ReportUnmappedGenericMethodCall(node, method))
            {
                return updatedNode;
            }

            if (ReportUnmappedExtensionMemberCall(node, method))
            {
                return updatedNode;
            }

            string metadataName = method.GetFullyQualifiedMetadataName();

            // Rewrite HLSL intrinsic methods
            if (method.IsStatic &&
                HlslKnownMethods.TryGetMappedName(metadataName, out string? mapping, out bool requiresParametersMapping))
            {
                if (requiresParametersMapping)
                {
                    mapping = HlslKnownMethods.GetMappedNameWithParameters(method.Name, method.Parameters.Select(static p => p.Type.Name));
                }

                // Allow specialized types to track the method invocation, if needed
                TrackKnownMethodInvocation(metadataName);

#if D3D12_SOURCE_GENERATOR
                // Refuse a matrix on an intrinsic with an out parameter (see ShaderSourceRewriter for more info)
                if (ReportMatrixOnIntrinsicWithOutParameter(node, method))
                {
                    return updatedNode;
                }
#endif

                // Handle named intrinsics (see ShaderSourceRewriter for more info)
                if (VisitKnownNamedIntrinsicInvocationExpression(node, updatedNode, mapping) is SyntaxNode namedIntrinsic)
                {
                    return namedIntrinsic;
                }

#if !D3D12_SOURCE_GENERATOR
                // Parenthesize the coordinate argument for D2D input sampling intrinsics (see ShaderSourceRewriter for more info)
                if (HlslKnownMethods.NeedsParenthesizedCoordinateArgument(metadataName))
                {
                    ExpressionSyntax coordinateExpression = updatedNode.ArgumentList.Arguments[1].Expression;

                    updatedNode = updatedNode.ReplaceNode(coordinateExpression, ParenthesizedExpression(coordinateExpression));
                }
#endif

                return updatedNode.WithExpression(IdentifierName(mapping!));
            }

            // A static method with no mapping is imported by rewriting its declaration, the same way the
            // shader body imports one. HLSL accepts a call in a static field initializer because every
            // forward declaration is written ahead of the static fields. A method on the shader type is
            // left alone, as the generator writes those out through its own path.
            if (method.IsStatic &&
                !SymbolEqualityComparer.Default.Equals(this.shaderType, method.ContainingType))
            {
                return VisitImportedStaticMethodInvocation(node, updatedNode, method);
            }
        }

        return updatedNode;
    }

    /// <summary>
    /// Imports the declaration of a static method called from an initializer, and renames the call to it.
    /// </summary>
    /// <param name="node">The original invocation.</param>
    /// <param name="updatedNode">The invocation as rewritten so far.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>The invocation, renamed to the imported declaration when one could be produced.</returns>
    private InvocationExpressionSyntax VisitImportedStaticMethodInvocation(
        InvocationExpressionSyntax node,
        InvocationExpressionSyntax updatedNode,
        IMethodSymbol method)
    {
        string methodIdentifier = method.GetFullyQualifiedMetadataName().ToHlslIdentifierName();

        if (!this.staticMethods.ContainsKey(method))
        {
            if (!method.TryGetSyntaxNode(CancellationToken, out MethodDeclarationSyntax? methodNode))
            {
                Diagnostics.Add(InvalidMethodOrConstructorCall, node, method);

                return updatedNode;
            }

            // Claim the entry before rewriting, so that a method reaching itself terminates
            this.staticMethods.Add(method, null!);

            ShaderSourceRewriter shaderSourceRewriter = CreateImportRewriter();

            MethodDeclarationSyntax processedMethod = shaderSourceRewriter.Visit(methodNode)!.WithoutTrivia();

            MergeImportedLocalFunctions(shaderSourceRewriter);

            this.staticMethods[method] = processedMethod.WithIdentifier(Identifier(methodIdentifier));
        }

        // C# leaves the receiver of an extension method out of the argument list, whereas the declaration
        // is imported with the receiver as its first parameter, so it is moved into place here
        if (SemanticModel.For(node).GetSymbolInfo(node, CancellationToken).Symbol is IMethodSymbol { ReducedFrom: not null } &&
            updatedNode.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            updatedNode = updatedNode.WithArgumentList(
                updatedNode.ArgumentList.WithArguments(
                    updatedNode.ArgumentList.Arguments.Insert(0, Argument(memberAccess.Expression))));
        }

        return updatedNode.WithExpression(IdentifierName(methodIdentifier));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A constructor declaration is a body, so it is imported by the rewriter that imports every other one.
    /// </remarks>
    protected override SyntaxNode VisitUserDefinedObjectCreationExpression(
        BaseObjectCreationExpressionSyntax node,
        BaseObjectCreationExpressionSyntax updatedNode,
        TypeSyntax targetType)
    {
        ShaderSourceRewriter shaderSourceRewriter = CreateImportRewriter();

        SyntaxNode rewrittenNode = shaderSourceRewriter.ImportUserDefinedConstructor(node, updatedNode, targetType);

        MergeImportedLocalFunctions(shaderSourceRewriter);

        return rewrittenNode;
    }

    /// <summary>
    /// Creates the rewriter that imports a declaration reached from the initializer being rewritten.
    /// </summary>
    /// <returns>
    /// A <see cref="ShaderSourceRewriter"/> instance sharing the collections this one accumulates into,
    /// the requirements of the shader among them.
    /// The local functions it lifts out are its own, which is what <see cref="MergeImportedLocalFunctions"/> carries over.
    /// </returns>
    private ShaderSourceRewriter CreateImportRewriter()
    {
        return new(
            this.shaderType,
            SemanticModel,
            DiscoveredTypes,
            this.staticMethods,
            instanceMethods,
            constructors,
            ConstantDefinitions,
            StaticFieldDefinitions,
            Requirements,
            Diagnostics,
            CancellationToken);
    }

    /// <summary>
    /// Carries over the local functions an import lifted out, to be written like any other.
    /// </summary>
    /// <param name="rewriter">The <see cref="ShaderSourceRewriter"/> instance that performed the import.</param>
    private void MergeImportedLocalFunctions(ShaderSourceRewriter rewriter)
    {
        foreach (KeyValuePair<IMethodSymbol, LocalFunctionStatementSyntax> localFunction in rewriter.LocalFunctions)
        {
            this.localFunctions[localFunction.Key] = localFunction.Value;
        }
    }

    /// <inheritdoc/>
    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        ArgumentSyntax updatedNode = (ArgumentSyntax)base.VisitArgument(node)!;

        updatedNode = updatedNode.WithRefKindKeyword(Token(SyntaxKind.None));

        return updatedNode;
    }

    /// <summary>
    /// Tracks a property access to a known HLSL property.
    /// </summary>
    /// <param name="operation">The <see cref="IMemberReferenceOperation"/> instance for the operation.</param>
    /// <param name="node">The <see cref="MemberAccessExpressionSyntax"/> instance for the operation.</param>
    private partial void TrackKnownPropertyAccess(IMemberReferenceOperation operation, MemberAccessExpressionSyntax node);

    /// <summary>
    /// Tracks a method invocation for a known HLSL method.
    /// </summary>
    /// <param name="metadataName">The metadata name of the method being invoked.</param>
    private partial void TrackKnownMethodInvocation(string metadataName);
}