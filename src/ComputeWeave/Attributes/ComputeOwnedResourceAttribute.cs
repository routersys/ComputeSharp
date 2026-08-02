using System;

namespace ComputeWeave;

/// <summary>
/// An attribute that binds a parameter of a compute pipeline method to an owned resource slot of its host.
/// </summary>
/// <param name="slotFieldName">The name of the owned resource slot field the parameter receives the resources of.</param>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ComputeOwnedResourceAttribute(string slotFieldName) : Attribute
{
    /// <summary>
    /// Gets the name of the owned resource slot field the parameter receives the resources of.
    /// </summary>
    public string SlotFieldName { get; } = slotFieldName;
}
