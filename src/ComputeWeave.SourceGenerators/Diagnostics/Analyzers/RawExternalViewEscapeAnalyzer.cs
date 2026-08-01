using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a raw external view escapes the scope it is valid in.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RawExternalViewEscapeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RawExternalViewEscape];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the scopes a raw external view can be obtained from
            if (context.Compilation.GetTypeByMetadataName("ComputeWeave.BorrowedExternalTextureView`1") is not { } borrowedViewSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeWeave.ExternalTextureLease`1") is not { } leaseSymbol)
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                IInvocationOperation operation = (IInvocationOperation)context.Operation;

                if (operation.TargetMethod.Name is not "DangerousGetView" ||
                    operation.Instance?.Type is not INamedTypeSymbol { IsGenericType: true } scopeTypeSymbol ||
                    (!SymbolEqualityComparer.Default.Equals(scopeTypeSymbol.OriginalDefinition, borrowedViewSymbol) &&
                     !SymbolEqualityComparer.Default.Equals(scopeTypeSymbol.OriginalDefinition, leaseSymbol)))
                {
                    return;
                }

                if (!IsEscaping(operation))
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    RawExternalViewEscape,
                    operation.Syntax.GetLocation(),
                    operation.TargetMethod));
            }, OperationKind.Invocation);
        });
    }

    /// <summary>
    /// Checks whether the result of a raw view accessor escapes the scope it was obtained in.
    /// </summary>
    /// <param name="operation">The raw view accessor invocation.</param>
    /// <returns>Whether the result of <paramref name="operation"/> escapes its scope.</returns>
    private static bool IsEscaping(IInvocationOperation operation)
    {
        IOperation? parent = operation.Parent;

        // A conversion inserted by the compiler does not change where the value ends up
        while (parent is IConversionOperation)
        {
            parent = parent.Parent;
        }

        return parent switch
        {
            ISimpleAssignmentOperation assignment => IsEscapingTarget(assignment.Target),
            IReturnOperation => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether assigning a raw view to a given target makes it outlive the scope it was obtained in.
    /// </summary>
    /// <param name="target">The target of the assignment.</param>
    /// <returns>Whether <paramref name="target"/> makes the assigned view escape.</returns>
    private static bool IsEscapingTarget(IOperation target)
    {
        return target switch
        {
            // Locals, discards and value parameters are all bound to the scope the view was obtained in
            ILocalReferenceOperation or IDiscardOperation => false,
            IParameterReferenceOperation parameter => parameter.Parameter.RefKind is not RefKind.None,
            _ => true
        };
    }
}
