using ComputeWeave.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotReadOnlyComputeShaderTypeWithFieldsAnalyzer : NotReadOnlyShaderTypeWithFieldsAnalyzerBase
{
    /// <summary>
    /// Creates a new <see cref="NotReadOnlyComputeShaderTypeWithFieldsAnalyzer"/> instance.
    /// </summary>
    public NotReadOnlyComputeShaderTypeWithFieldsAnalyzer()
        : base(NotReadOnlyShaderType, ["ComputeWeave.IComputeShader", "ComputeWeave.IComputeShader`1"])
    {
    }
}