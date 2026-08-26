using System.Composition;
using ComputeWeave.CodeFixing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.D2D1.CodeFixers;

/// <summary>
/// A code fixer that adds the <c>[D2DGeneratedPixelShaderDescriptor]</c> to D2D1 shader types with no descriptor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class MissingPixelShaderDescriptorOnPixelShaderCodeFixer : MissingAttributeCodeFixer
{
    /// <summary>
    /// The set of type names for all D2D attributes that can be over shader types.
    /// </summary>
    private static readonly string[] D2DAttributeTypeNames =
    [
        "ComputeWeave.D2D1.D2DCompileOptionsAttribute",
        "ComputeWeave.D2D1.D2DEffectAuthorAttribute",
        "ComputeWeave.D2D1.D2DEffectCategoryAttribute",
        "ComputeWeave.D2D1.D2DEffectDescriptionAttribute",
        "ComputeWeave.D2D1.D2DEffectDisplayNameAttribute",
        "ComputeWeave.D2D1.D2DEffectIdAttribute",
        "ComputeWeave.D2D1.D2DInputComplexAttribute",
        "ComputeWeave.D2D1.D2DInputCountAttribute",
        "ComputeWeave.D2D1.D2DInputDescriptionAttribute",
        "ComputeWeave.D2D1.D2DInputSimpleAttribute",
        "ComputeWeave.D2D1.D2DOutputBufferAttribute",
        "ComputeWeave.D2D1.D2DPixelOptionsAttribute",
        "ComputeWeave.D2D1.D2DRequiresScenePositionAttribute",
        "ComputeWeave.D2D1.D2DShaderProfileAttribute"
    ];

    /// <summary>
    /// Creates a new <see cref="MissingAttributeCodeFixer"/> instance with the specified parameters.
    /// </summary>
    public MissingPixelShaderDescriptorOnPixelShaderCodeFixer()
        : base(
            diagnosticId: MissingPixelShaderDescriptorOnPixelShaderTypeId,
            codeActionTitle: "Add [D2DGeneratedPixelShaderDescriptor] attribute",
            attributeFullyQualifiedMetadataName: "ComputeWeave.D2D1.D2DGeneratedPixelShaderDescriptorAttribute",
            leadingAttributeFullyQualifiedMetadataNames: D2DAttributeTypeNames)
    {
    }
}