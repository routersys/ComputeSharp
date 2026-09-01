using System;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Mappings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ComputeWeave.SourceGeneration.SyntaxRewriters;

/// <inheritdoc/>
partial class ShaderSourceRewriter
{
    /// <summary>
    /// Gets whether or not the shader uses <see cref="GroupIds"/> at least once (except <see cref="GroupIds.Index"/>).
    /// </summary>
    public bool IsGroupIdsUsed { get; private set; }

    /// <summary>
    /// Gets whether or not the shader uses <see cref="GroupIds.Index"/> at least once.
    /// </summary>
    public bool IsGroupIdsIndexUsed { get; private set; }

    /// <summary>
    /// Gets whether or not the shader uses <see cref="GridIds"/> at least once.
    /// </summary>
    public bool IsGridIdsUsed { get; set; }

    /// <summary>
    /// Gets whether or not the shader uses a texture sampler at least once.
    /// </summary>
    public bool IsSamplerUsed { get; private set; }

    /// <summary>
    /// Gets whether or not the shader waits for the whole thread group at least once.
    /// </summary>
    public bool SynchronizesTheWholeThreadGroup { get; private set; }

    /// <inheritdoc/>
    private partial SyntaxNode RewriteSampledTextureAccess(IInvocationOperation operation, ExpressionSyntax expression, ArgumentSyntax arguments)
    {
        IsSamplerUsed = true;

        // Transform a method invocation syntax into a sampling call with the implicit static linear sampler.
        // For instance: texture.Sample(uv) will be rewritten as texture.SampleLevel(__sampler, uv, 0).
        return
            InvocationExpression(((MemberAccessExpressionSyntax)expression).WithName(IdentifierName("SampleLevel")))
            .AddArgumentListArguments(
                Argument(IdentifierName("__sampler")),
                arguments,
                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))));
    }

    /// <inheritdoc/>
    partial void TrackKnownMethodInvocation(string metadataName)
    {
        SynchronizesTheWholeThreadGroup |= HlslKnownMethods.SynchronizesTheWholeThreadGroup(metadataName);
    }

    /// <inheritdoc/>
    partial void TrackNestedRewriter(ShaderSourceRewriter rewriter)
    {
        SynchronizesTheWholeThreadGroup |= rewriter.SynchronizesTheWholeThreadGroup;
    }

    /// <inheritdoc/>
    partial void TrackKnownPropertyAccess(IMemberReferenceOperation operation, MemberAccessExpressionSyntax node, string mappedName)
    {
        // Mark which dispatch properties have been used, to optimize the declaration afterwards
        if (operation.Member.IsStatic)
        {
            string typeName = operation.Member.ContainingType.GetFullyQualifiedMetadataName();

            if (mappedName == $"__{nameof(GroupIds)}__get_Index")
            {
                IsGroupIdsIndexUsed = true;
            }
            else if (typeName == typeof(GroupIds).FullName)
            {
                IsGroupIdsUsed = true;
            }
            else if (typeName == typeof(GridIds).FullName)
            {
                IsGridIdsUsed = true;
            }

            // Check that the dispatch info types are only used from the main shader body
            if (!this.isEntryPoint || this.localFunctionDepth > 0)
            {
                DiagnosticDescriptor? descriptor = typeName switch
                {
                    _ when typeName == typeof(ThreadIds).FullName || typeName == typeof(ThreadIds.Normalized).FullName => InvalidThreadIdsUsage,
                    _ when typeName == typeof(GroupIds).FullName => InvalidGroupIdsUsage,
                    _ when typeName == typeof(GroupSize).FullName => InvalidGroupSizeUsage,
                    _ when typeName == typeof(GridIds).FullName => InvalidGridIdsUsage,
                    _ when typeName == typeof(DispatchSize).FullName => InvalidDispatchSizeUsage,
                    _ => null
                };

                if (descriptor is not null)
                {
                    Diagnostics.Add(descriptor, node);
                }
            }
        }
    }

    /// <summary>
    /// Reports an intrinsic that writes through an out parameter being given an integer matrix.
    /// </summary>
    /// <param name="node">The invocation that is about to be written out as it stands.</param>
    /// <param name="method">The resolved target of <paramref name="node"/>.</param>
    /// <returns>Whether the invocation was reported, in which case the caller leaves it alone.</returns>
    /// <remarks>
    /// <para>
    /// DXC terminates with an access violation on this combination, taking the compiler process with it, so the
    /// build fails with a native fatal error naming no source line. Reporting here keeps the call from reaching
    /// the compiler at all, the compilation step being skipped for a shader whose rewriting produced an error,
    /// which is what lets the author see their own line instead of a stack trace.
    /// </para>
    /// <para>
    /// The condition is the shape of the signature rather than a name, because modf, frexp and sincos were each
    /// measured to terminate the same way. Only Modf declares integer overloads today, so it is the only one
    /// that can be reached, but an integer overload added to either of the others is refused without this having
    /// to be revisited. A floating point matrix compiles, and so does an integer vector, so neither is touched.
    /// </para>
    /// </remarks>
    private bool ReportIntegerMatrixOnIntrinsicWithOutParameter(InvocationExpressionSyntax node, IMethodSymbol method)
    {
        bool hasOutParameter = false;
        bool hasIntegerMatrix = false;

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            hasOutParameter |= parameter.RefKind == RefKind.Out;
            hasIntegerMatrix |= IsIntegerMatrixType(parameter.Type);
        }

        if (!hasOutParameter || !hasIntegerMatrix)
        {
            return false;
        }

        Diagnostics.Add(IntegerMatrixOnIntrinsicWithOutParameter, node, method.Name);

        return true;

        // The caller has already matched a mapped intrinsic, whose parameters are the shader
        // primitives, so the name alone tells an integer matrix from anything else
        static bool IsIntegerMatrixType(ITypeSymbol type)
        {
            string name = type.Name;

            int digits = name.StartsWith("Int", StringComparison.Ordinal) ? 3
                : name.StartsWith("UInt", StringComparison.Ordinal) ? 4
                : 0;

            return digits > 0
                && name.Length == digits + 3
                && name[digits] is >= '1' and <= '4'
                && name[digits + 1] == 'x'
                && name[digits + 2] is >= '1' and <= '4';
        }
    }
}