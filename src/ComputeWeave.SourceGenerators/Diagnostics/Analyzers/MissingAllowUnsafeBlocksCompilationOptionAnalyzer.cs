using ComputeWeave.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingAllowUnsafeBlocksCompilationOptionAnalyzer : MissingAllowUnsafeBlocksCompilationOptionAnalyzerBase
{
    /// <summary>
    /// Creates a new <see cref="MissingAllowUnsafeBlocksCompilationOptionAnalyzer"/> instance.
    /// </summary>
    public MissingAllowUnsafeBlocksCompilationOptionAnalyzer()
        : base(MissingAllowUnsafeBlocksOption, "ComputeWeave.GeneratedComputeShaderDescriptorAttribute")
    {
    }
}