using ComputeWeave.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.D2D1.SourceGenerators;

/// <inheritdoc/>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotAccessibleD2DGeneratedPixelShaderDescriptorAttributeTargetAnalyzer : NotAccessibleGeneratedShaderDescriptorAttributeTargetAnalyzerBase
{
    /// <summary>
    /// Creates a new <see cref="NotAccessibleD2DGeneratedPixelShaderDescriptorAttributeTargetAnalyzer"/> instance.
    /// </summary>
    public NotAccessibleD2DGeneratedPixelShaderDescriptorAttributeTargetAnalyzer()
        : base(NotAccessibleTargetTypeForD2DGeneratedPixelShaderDescriptorAttribute, "ComputeWeave.D2D1.D2DGeneratedPixelShaderDescriptorAttribute")
    {
    }
}