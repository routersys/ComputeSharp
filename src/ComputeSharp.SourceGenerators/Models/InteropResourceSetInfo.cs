using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A model representing all necessary info for a full generation pass for a compute interop resource set.
/// </summary>
/// <param name="Hierarchy">The hierarchy info for the annotated type.</param>
/// <param name="TypeName">The name of the annotated type.</param>
/// <param name="Descriptor">The canonical binary descriptor of the annotated type.</param>
/// <param name="Slots">The shared texture slots of the annotated type, in canonical slot ordinal order.</param>
internal sealed record InteropResourceSetInfo(
    HierarchyInfo Hierarchy,
    string TypeName,
    EquatableArray<byte> Descriptor,
    EquatableArray<SharedTextureSlotSyntaxInfo> Slots);

/// <summary>
/// A model representing the members generated for a single shared texture slot of a compute interop resource set.
/// </summary>
/// <param name="CanonicalName">The generated canonical name of the slot.</param>
/// <param name="FieldName">The name of the resource set field declaring the slot.</param>
/// <param name="SlotTypeName">The name of the slot type constructed by the generated factory.</param>
/// <param name="BindingTypeName">The bound texture type returned by the generated binding accessor.</param>
/// <param name="ViewTypeName">The external view type returned by the generated borrow and lease accessors.</param>
/// <param name="BindingAccessibility">The accessibility keyword of the generated binding accessor.</param>
/// <param name="ViewAccessibility">The accessibility keyword of the generated borrow and lease accessors.</param>
internal sealed record SharedTextureSlotSyntaxInfo(
    string CanonicalName,
    string FieldName,
    string SlotTypeName,
    string BindingTypeName,
    string ViewTypeName,
    string BindingAccessibility,
    string ViewAccessibility);
