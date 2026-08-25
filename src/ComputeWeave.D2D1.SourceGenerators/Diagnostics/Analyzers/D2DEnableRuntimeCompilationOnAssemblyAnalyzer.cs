using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.D2D1.SourceGenerators;

/// <summary>
/// A diagnostic analyzer that generates diagnostics for the [D2DEnableRuntimeCompilation] attribute, when used on an assembly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class D2DEnableRuntimeCompilationOnAssemblyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [D2DRuntimeCompilationOnAssemblyNotNecessary];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(static context =>
        {
            IAssemblySymbol assemblySymbol = context.Compilation.Assembly;

            // Emit a diagnostic if an assembly has both [D2DShaderProfile] and [D2DEnableRuntimeCompilation] (the latter is useless)
            if (assemblySymbol.TryGetAttributeWithFullyQualifiedMetadataName("ComputeWeave.D2D1.D2DShaderProfileAttribute", out _) &&
                assemblySymbol.TryGetAttributeWithFullyQualifiedMetadataName("ComputeWeave.D2D1.D2DEnableRuntimeCompilationAttribute", out AttributeData? attributeData))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    D2DRuntimeCompilationOnAssemblyNotNecessary,
                    attributeData.GetLocation(),
                    assemblySymbol));
            }
        });
    }
}