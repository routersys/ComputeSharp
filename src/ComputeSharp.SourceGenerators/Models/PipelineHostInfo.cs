using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A model representing all necessary info for a full generation pass for a compute pipeline host.
/// </summary>
/// <param name="Hierarchy">The hierarchy info for the annotated type.</param>
/// <param name="TypeName">The name of the annotated type.</param>
/// <param name="Descriptor">The canonical binary descriptor of the annotated type.</param>
/// <param name="DeviceFieldName">The name of the device field owned by the annotated type.</param>
/// <param name="Plans">The exact resource plans of the annotated type, in canonical slot ordinal order.</param>
/// <param name="Slots">The owned slots of the annotated type, in canonical slot ordinal order.</param>
/// <param name="Invocations">The pipeline invocations of the annotated type, in canonical pipeline ordinal order.</param>
internal sealed record PipelineHostInfo(
    HierarchyInfo Hierarchy,
    string TypeName,
    EquatableArray<byte> Descriptor,
    string DeviceFieldName,
    EquatableArray<ResourcePlanInfo> Plans,
    EquatableArray<OwnedSlotSyntaxInfo> Slots,
    EquatableArray<PipelineInvocationSyntaxInfo> Invocations);

/// <summary>
/// A model representing the members generated for a single owned slot of a compute pipeline host.
/// </summary>
/// <param name="Ordinal">The 0-based ordinal of the slot within its host.</param>
/// <param name="CanonicalName">The generated canonical name of the slot.</param>
/// <param name="FieldName">The name of the host field declaring the slot.</param>
/// <param name="PlanTypeName">The name of the plan type accepted by the generated typed method.</param>
/// <param name="MaterializerTypeName">The name of the generated materializer type.</param>
/// <param name="PlanAccessibility">The accessibility keyword of the generated typed plan method.</param>
/// <param name="BindingAccessibility">The accessibility keyword of the generated binding accessor.</param>
/// <param name="BindingResourceTypeName">The bound resource type, or <see langword="null"/> for a resource group slot.</param>
/// <param name="RequiresDoublePrecisionSupport">Whether any owned resource stores double precision floating point numbers.</param>
/// <param name="PlanFields">The plan fields, in canonical field ordinal order.</param>
/// <param name="Resources">The owned resources, in slot resource index order.</param>
internal sealed record OwnedSlotSyntaxInfo(
    uint Ordinal,
    string CanonicalName,
    string FieldName,
    string PlanTypeName,
    string MaterializerTypeName,
    string PlanAccessibility,
    string BindingAccessibility,
    string? BindingResourceTypeName,
    bool RequiresDoublePrecisionSupport,
    EquatableArray<ResourcePlanFieldInfo> PlanFields,
    EquatableArray<SlotResourceSyntaxInfo> Resources);

/// <summary>
/// A model representing a single resource declaration written into a generated materializer.
/// </summary>
/// <param name="Shape">The declaration shape of the resource.</param>
/// <param name="ElementTypeName">The fully qualified name of the element type.</param>
/// <param name="PixelTypeName">The fully qualified name of the pixel type, for normalized textures only.</param>
/// <param name="DimensionParameterNames">The plan parameters carrying the dimensions, in declaration order.</param>
internal sealed record SlotResourceSyntaxInfo(
    ResourcePlanKind Shape,
    string ElementTypeName,
    string? PixelTypeName,
    EquatableArray<string> DimensionParameterNames);
