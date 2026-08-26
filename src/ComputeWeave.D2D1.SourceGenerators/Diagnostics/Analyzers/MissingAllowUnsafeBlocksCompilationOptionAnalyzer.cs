using ComputeWeave.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.D2D1.SourceGenerators;

/// <inheritdoc/>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingAllowUnsafeBlocksCompilationOptionAnalyzer : MissingAllowUnsafeBlocksCompilationOptionAnalyzerBase
{
    /// <summary>
    /// Creates a new <see cref="MissingAllowUnsafeBlocksCompilationOptionAnalyzer"/> instance.
    /// </summary>
    public MissingAllowUnsafeBlocksCompilationOptionAnalyzer()
        : base(MissingAllowUnsafeBlocksOption, "ComputeWeave.D2D1.D2DGeneratedPixelShaderDescriptorAttribute")
    {
    }
}