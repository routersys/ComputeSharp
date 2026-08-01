using System.Composition;
using ComputeWeave.CodeFixing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using static ComputeWeave.SourceGeneration.Diagnostics.DiagnosticDescriptors;

namespace ComputeWeave.CodeFixers;

/// <summary>
/// A code fixer that adds the <c>[GeneratedComputeShaderDescriptor]</c> to compute shader types with no descriptor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class MissingComputeShaderDescriptorOnComputeShaderCodeFixer : MissingAttributeCodeFixer
{
    /// <summary>
    /// The set of type names for all attributes that can be over shader types.
    /// </summary>
    private static readonly string[] AttributeTypeNames =
    [
        "ComputeWeave.ThreadGroupSizeAttribute",
        "ComputeWeave.GroupSharedAttribute"
    ];

    /// <summary>
    /// Creates a new <see cref="MissingAttributeCodeFixer"/> instance with the specified parameters.
    /// </summary>
    public MissingComputeShaderDescriptorOnComputeShaderCodeFixer()
        : base(
            diagnosticId: MissingComputeShaderDescriptorOnComputeShaderTypeId,
            codeActionTitle: "Add [GeneratedComputeShaderDescriptor] attribute",
            attributeFullyQualifiedMetadataName: "ComputeWeave.GeneratedComputeShaderDescriptorAttribute",
            leadingAttributeFullyQualifiedMetadataNames: AttributeTypeNames)
    {
    }
}