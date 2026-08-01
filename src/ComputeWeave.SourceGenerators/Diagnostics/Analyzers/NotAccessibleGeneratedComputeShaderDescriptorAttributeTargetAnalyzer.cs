using ComputeWeave.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotAccessibleGeneratedComputeShaderDescriptorAttributeTargetAnalyzer : NotAccessibleGeneratedShaderDescriptorAttributeTargetAnalyzerBase
{
    /// <summary>
    /// Creates a new <see cref="NotAccessibleGeneratedComputeShaderDescriptorAttributeTargetAnalyzer"/> instance.
    /// </summary>
    public NotAccessibleGeneratedComputeShaderDescriptorAttributeTargetAnalyzer()
        : base(NotAccessibleTargetTypeForGeneratedComputeShaderDescriptorAttribute, "ComputeWeave.GeneratedComputeShaderDescriptorAttribute")
    {
    }
}