using System.Collections.Immutable;
using ComputeSharp.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static ComputeSharp.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates an error whenever a compute pipeline uploads from CPU memory.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedTransientCpuUploadAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UnsupportedTransientCpuUploadInPipeline];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the symbols the pipeline methods and the graphics resources are recognized with
            if (context.Compilation.GetTypeByMetadataName("ComputeSharp.ComputePipelineAttribute") is not { } pipelineAttributeSymbol ||
                context.Compilation.GetTypeByMetadataName("ComputeSharp.IGraphicsResource") is not { } graphicsResourceSymbol)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(context =>
            {
                if (context.OwningSymbol is not IMethodSymbol methodSymbol ||
                    !methodSymbol.HasAttributeWithType(pipelineAttributeSymbol))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    IInvocationOperation operation = (IInvocationOperation)context.Operation;

                    if (!IsCpuUpload(operation, graphicsResourceSymbol))
                    {
                        return;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedTransientCpuUploadInPipeline,
                        operation.Syntax.GetLocation(),
                        methodSymbol,
                        operation.TargetMethod));
                }, OperationKind.Invocation);
            });
        });
    }

    /// <summary>
    /// Checks whether an invocation uploads the contents of a graphics resource from CPU memory.
    /// </summary>
    /// <param name="operation">The invocation to check.</param>
    /// <param name="graphicsResourceSymbol">The <c>IGraphicsResource</c> symbol.</param>
    /// <returns>Whether <paramref name="operation"/> uploads from CPU memory.</returns>
    private static bool IsCpuUpload(IInvocationOperation operation, INamedTypeSymbol graphicsResourceSymbol)
    {
        IMethodSymbol targetMethod = operation.TargetMethod;

        if (targetMethod.Name is not "CopyFrom")
        {
            return false;
        }

        // An extension method invoked in its unreduced form declares the receiver as its first
        // parameter, so the copy destination and the sources have to be resolved from there
        ITypeSymbol? receiverTypeSymbol = operation.Instance?.Type;
        int firstSourceIndex = 0;

        if (receiverTypeSymbol is null && targetMethod is { IsExtensionMethod: true, Parameters.Length: > 0 })
        {
            receiverTypeSymbol = targetMethod.Parameters[0].Type;
            firstSourceIndex = 1;
        }

        // Only the copy APIs writing into a graphics resource can allocate a transient upload resource
        if (receiverTypeSymbol is null || !receiverTypeSymbol.HasInterfaceWithType(graphicsResourceSymbol))
        {
            return false;
        }

        // The overloads reading from another graphics resource stay on the GPU, so only the ones
        // reading from a CPU buffer are rejected. Those are the array and span based overloads
        for (int i = firstSourceIndex; i < targetMethod.Parameters.Length; i++)
        {
            if (IsCpuBuffer(targetMethod.Parameters[i].Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a type is a CPU buffer a graphics resource can be uploaded from.
    /// </summary>
    /// <param name="typeSymbol">The type to check.</param>
    /// <returns>Whether <paramref name="typeSymbol"/> is a CPU buffer.</returns>
    private static bool IsCpuBuffer(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind is TypeKind.Array)
        {
            return true;
        }

        if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
        {
            return false;
        }

        return namedTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName("System.Span`1") ||
               namedTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName("System.ReadOnlySpan`1") ||
               namedTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName("System.Memory`1") ||
               namedTypeSymbol.OriginalDefinition.HasFullyQualifiedMetadataName("System.ReadOnlyMemory`1");
    }
}
