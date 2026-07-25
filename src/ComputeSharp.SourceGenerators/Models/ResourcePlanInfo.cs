using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;

namespace ComputeSharp.SourceGenerators.Models;

/// <summary>
/// A model representing an exact resource plan type to generate.
/// </summary>
/// <param name="TypeName">The name of the generated plan type.</param>
/// <param name="Fields">The plan fields, in canonical field ordinal order.</param>
internal sealed record ResourcePlanInfo(string TypeName, EquatableArray<ResourcePlanFieldInfo> Fields);

/// <summary>
/// A model representing a single scalar dimension of a generated resource plan type.
/// </summary>
/// <param name="ParameterName">The name of the generated constructor parameter.</param>
/// <param name="PropertyName">The name of the generated property.</param>
internal sealed record ResourcePlanFieldInfo(string ParameterName, string PropertyName);

/// <summary>
/// A model representing all necessary info for a full generation pass for a resource group plan.
/// </summary>
/// <param name="Hierarchy">The hierarchy info for the annotated type.</param>
/// <param name="Plan">The exact resource plan of the annotated type.</param>
internal sealed record ResourceGroupPlanInfo(HierarchyInfo Hierarchy, ResourcePlanInfo Plan);
