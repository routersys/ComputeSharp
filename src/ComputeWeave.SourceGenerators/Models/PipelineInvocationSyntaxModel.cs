using ComputeWeave.SourceGeneration.Helpers;

namespace ComputeWeave.SourceGenerators.Models;

/// <summary>
/// The way a generated pipeline invocation reaches the resource it pins.
/// </summary>
internal enum PipelineBindingKind
{
    /// <summary>
    /// The resource is a parameter of the pipeline method.
    /// </summary>
    Parameter,

    /// <summary>
    /// The resource is a parameter of the pipeline method shared with an external queue.
    /// </summary>
    ExternalParameter,

    /// <summary>
    /// The resource is a borrowed field of the host.
    /// </summary>
    BorrowedField,

    /// <summary>
    /// The resource is owned by a slot of the host.
    /// </summary>
    OwnedSlot
}

/// <summary>
/// A model representing a single pin written into a generated pipeline invocation.
/// </summary>
/// <param name="Kind">The way the pinned resource is reached.</param>
/// <param name="Name">The name of the parameter or of the host field carrying the resource.</param>
/// <param name="ResourceTypeName">The fully qualified name of the pinned resource type.</param>
/// <param name="SlotOrdinal">The ordinal of the owning slot, for slot owned resources.</param>
/// <param name="SlotResourceIndex">The index within the owning slot, for slot owned resources.</param>
/// <param name="IsResolved">Whether the pinned resource is passed to the pipeline method.</param>
internal sealed record PipelineBindingSyntaxInfo(
    PipelineBindingKind Kind,
    string Name,
    string ResourceTypeName,
    uint SlotOrdinal,
    uint SlotResourceIndex,
    bool IsResolved = false);

/// <summary>
/// The way a generated pipeline invocation produces a single argument of the pipeline method.
/// </summary>
internal enum PipelineArgumentKind
{
    /// <summary>
    /// The argument is a parameter of the generated overload.
    /// </summary>
    Parameter,

    /// <summary>
    /// The argument is the resource an external binding parameter was pinned to.
    /// </summary>
    ExternalResource,

    /// <summary>
    /// The argument is the resources owned by a slot of the host.
    /// </summary>
    OwnedResource
}

/// <summary>
/// A model representing a single argument passed to a pipeline method by a generated invocation.
/// </summary>
/// <param name="Kind">The way the argument is produced.</param>
/// <param name="Name">The name of the pipeline method parameter receiving the argument.</param>
/// <param name="IsReadOnlyReference">Whether the argument is passed by readonly reference.</param>
internal sealed record PipelineArgumentSyntaxInfo(
    PipelineArgumentKind Kind,
    string Name,
    bool IsReadOnlyReference);

/// <summary>
/// A model representing a single owned resource parameter of a pipeline method.
/// </summary>
/// <param name="ParameterName">The name of the pipeline method parameter receiving the owned resources.</param>
/// <param name="TypeName">The fully qualified name of the resource or resource group type the parameter receives.</param>
/// <param name="GenerationFieldName">The host field caching the resolved group generation, or <see langword="null"/> for a single resource.</param>
/// <param name="BindingIndices">The indices of the pins carrying the owned resources, in slot resource index order.</param>
internal sealed record PipelineOwnedResourceSyntaxInfo(
    string ParameterName,
    string TypeName,
    string? GenerationFieldName,
    EquatableArray<int> BindingIndices);

/// <summary>
/// A model representing a single parameter of a generated pipeline overload.
/// </summary>
/// <param name="TypeName">The fully qualified name of the parameter type.</param>
/// <param name="ParameterName">The name of the parameter.</param>
/// <param name="IsReadOnlyReference">Whether the parameter is passed by readonly reference.</param>
/// <param name="BoundResourceTypeName">The fully qualified name of the resource an external binding parameter refers to, or <see langword="null"/>.</param>
internal sealed record PipelineParameterSyntaxInfo(
    string TypeName,
    string ParameterName,
    bool IsReadOnlyReference,
    string? BoundResourceTypeName);

/// <summary>
/// A model representing the members generated for a single pipeline method of a compute pipeline host.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the pipeline within its host.</param>
/// <param name="MethodName">The name of the declared pipeline method.</param>
/// <param name="InvocationTypeName">The name of the generated invocation type.</param>
/// <param name="Accessibility">The accessibility keyword of the generated overload.</param>
/// <param name="Parameters">The parameters of the generated overload, in declaration order.</param>
/// <param name="Bindings">The pins of the invocation, in contract ordinal order.</param>
/// <param name="Arguments">The arguments passed to the pipeline method, in declaration order.</param>
/// <param name="OwnedResources">The owned resource parameters of the pipeline method, in declaration order.</param>
internal sealed record PipelineInvocationSyntaxInfo(
    uint Ordinal,
    string MethodName,
    string InvocationTypeName,
    string Accessibility,
    EquatableArray<PipelineParameterSyntaxInfo> Parameters,
    EquatableArray<PipelineBindingSyntaxInfo> Bindings,
    EquatableArray<PipelineArgumentSyntaxInfo> Arguments,
    EquatableArray<PipelineOwnedResourceSyntaxInfo> OwnedResources);
