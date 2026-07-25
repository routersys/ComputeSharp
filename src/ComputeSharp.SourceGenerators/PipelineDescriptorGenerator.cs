using System.Collections.Immutable;
using System.Threading;
using ComputeSharp.SourceGeneration.Constants;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;
using ComputeSharp.SourceGenerators.Helpers;
using ComputeSharp.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComputeSharp.SourceGenerators;

/// <summary>
/// A source generator creating the canonical descriptors and the exact resource plans of compute pipeline hosts,
/// interop resource sets and resource groups.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class PipelineDescriptorGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The name of generator to include in the generated code.
    /// </summary>
    private const string GeneratorName = "ComputeSharp.PipelineDescriptorGenerator";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<PipelineDescriptorInfo> hostInfo =
            context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ComputeSharp.ComputePipelineHostAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, token) => GetHostInfo(context, token))
            .WithTrackingName(WellKnownTrackingNames.Execute)
            .Where(static item => item is not null)!;

        IncrementalValuesProvider<PipelineDescriptorInfo> resourceSetInfo =
            context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ComputeSharp.ComputeInteropResourceSetAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, token) => GetResourceSetInfo(context, token))
            .WithTrackingName(WellKnownTrackingNames.Execute)
            .Where(static item => item is not null)!;

        IncrementalValuesProvider<ResourceGroupPlanInfo> resourceGroupInfo =
            context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ComputeSharp.ComputeResourceGroupAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, token) => GetResourceGroupInfo(context, token))
            .WithTrackingName(WellKnownTrackingNames.Execute)
            .Where(static item => item is not null)!;

        context.RegisterSourceOutput(
            hostInfo.WithTrackingName(WellKnownTrackingNames.Output),
            static (context, item) => Emit(context, item));

        context.RegisterSourceOutput(
            resourceSetInfo.WithTrackingName(WellKnownTrackingNames.Output),
            static (context, item) => Emit(context, item));

        context.RegisterSourceOutput(
            resourceGroupInfo.WithTrackingName(WellKnownTrackingNames.Output),
            static (context, item) => Emit(context, item));
    }

    /// <summary>
    /// Gets the descriptor info for a candidate pipeline host type.
    /// </summary>
    /// <param name="context">The current generator context.</param>
    /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
    /// <returns>The descriptor info for the candidate type, if it declares a valid contract.</returns>
    private static PipelineDescriptorInfo? GetHostInfo(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { IsGenericType: false } typeSymbol ||
            !PipelineWellKnownSymbols.TryCreate(context.SemanticModel.Compilation, out PipelineWellKnownSymbols? symbols) ||
            !PipelineHostContractModelBuilder.TryBuild(typeSymbol, symbols, out PipelineHostContractInfo host) ||
            !ResourcePlanModelBuilder.TryBuildHostPlans(host, out EquatableArray<ResourcePlanInfo> plans))
        {
            return null;
        }

        token.ThrowIfCancellationRequested();

        return new PipelineDescriptorInfo(
            HierarchyInfo.From(typeSymbol),
            ImmutableArray.Create(PipelineDescriptorWriter.Write(host)),
            plans);
    }

    /// <summary>
    /// Gets the descriptor info for a candidate interop resource set type.
    /// </summary>
    /// <param name="context">The current generator context.</param>
    /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
    /// <returns>The descriptor info for the candidate type, if it declares a valid contract.</returns>
    private static PipelineDescriptorInfo? GetResourceSetInfo(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { IsGenericType: false } typeSymbol ||
            !PipelineWellKnownSymbols.TryCreate(context.SemanticModel.Compilation, out PipelineWellKnownSymbols? symbols) ||
            !InteropResourceSetContractModelBuilder.TryBuild(typeSymbol, symbols, out InteropResourceSetContractInfo resourceSet))
        {
            return null;
        }

        token.ThrowIfCancellationRequested();

        return new PipelineDescriptorInfo(
            HierarchyInfo.From(typeSymbol),
            ImmutableArray.Create(PipelineDescriptorWriter.Write(resourceSet)),
            ImmutableArray<ResourcePlanInfo>.Empty);
    }

    /// <summary>
    /// Gets the plan info for a candidate resource group type.
    /// </summary>
    /// <param name="context">The current generator context.</param>
    /// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
    /// <returns>The plan info for the candidate type, if it declares a valid contract.</returns>
    private static ResourceGroupPlanInfo? GetResourceGroupInfo(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            !PipelineWellKnownSymbols.TryCreate(context.SemanticModel.Compilation, out PipelineWellKnownSymbols? symbols) ||
            !ResourceGroupContractModelBuilder.TryBuild(typeSymbol, symbols, out ResourceGroupContractInfo group) ||
            !ResourcePlanModelBuilder.TryBuildGroupPlan(group, out ResourcePlanInfo plan))
        {
            return null;
        }

        token.ThrowIfCancellationRequested();

        return new ResourceGroupPlanInfo(HierarchyInfo.From(typeSymbol), plan);
    }

    /// <summary>
    /// Emits the generated source for a given descriptor info.
    /// </summary>
    /// <param name="context">The current source production context.</param>
    /// <param name="item">The descriptor info to emit the source for.</param>
    private static void Emit(SourceProductionContext context, PipelineDescriptorInfo item)
    {
        using IndentedTextWriter writer = new();
        using ImmutableArrayBuilder<IndentedTextWriter.Callback<PipelineDescriptorInfo>> callbacks = new();

        callbacks.Add(WriteCanonicalDescriptor);

        if (!item.Plans.IsEmpty)
        {
            callbacks.Add(WriteResourcePlans);
        }

        item.Hierarchy.WriteSyntax(item, writer, [], callbacks.WrittenSpan);

        context.AddSource($"{item.Hierarchy.FullyQualifiedMetadataName}.g.cs", writer.ToString());
    }

    /// <summary>
    /// Emits the generated source for a given resource group plan info.
    /// </summary>
    /// <param name="context">The current source production context.</param>
    /// <param name="item">The plan info to emit the source for.</param>
    private static void Emit(SourceProductionContext context, ResourceGroupPlanInfo item)
    {
        using IndentedTextWriter writer = new();

        item.Hierarchy.WriteSyntax(item, writer, [], [static (item, writer) => WriteResourcePlan(item.Plan, writer)]);

        context.AddSource($"{item.Hierarchy.FullyQualifiedMetadataName}.g.cs", writer.ToString());
    }

    /// <summary>
    /// Writes the canonical descriptor member of a given descriptor info.
    /// </summary>
    /// <param name="item">The descriptor info to write the member for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteCanonicalDescriptor(PipelineDescriptorInfo item, IndentedTextWriter writer)
    {
        writer.WriteLine("/// <summary>The canonical binary descriptor of the current type.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.Write("private static global::System.ReadOnlySpan<byte> CanonicalDescriptor => [");

        SyntaxFormattingHelper.WriteByteArrayInitializationExpressions(item.Descriptor.AsImmutableArray().AsSpan(), writer);

        writer.WriteLine("];");
    }

    /// <summary>
    /// Writes the exact resource plan types of a given descriptor info.
    /// </summary>
    /// <param name="item">The descriptor info to write the plan types for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourcePlans(PipelineDescriptorInfo item, IndentedTextWriter writer)
    {
        writer.WriteLineSeparatedMembers(
            item.Plans.AsImmutableArray().AsSpan(),
            static (plan, writer) => WriteResourcePlan(plan, writer));
    }
}
