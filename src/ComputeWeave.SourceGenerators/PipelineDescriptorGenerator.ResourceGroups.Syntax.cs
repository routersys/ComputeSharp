using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGenerators.Helpers;
using ComputeWeave.SourceGenerators.Models;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
partial class PipelineDescriptorGenerator
{
    /// <summary>
    /// Writes the constructor of a single resource group.
    /// </summary>
    /// <param name="item">The resource group to write the constructor for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceGroupConstructor(ResourceGroupPlanInfo item, IndentedTextWriter writer)
    {
        string typeName = item.Hierarchy.Hierarchy[0].QualifiedName;

        writer.WriteLine($"""/// <summary>Creates a new <see cref="{typeName}"/> instance with the specified parameters.</summary>""");

        foreach (ResourceGroupMemberInfo member in item.Members)
        {
            writer.WriteLine($"""/// <param name="{member.ParameterName}">The resource owned by <c>{member.PropertyName}</c>.</param>""");
        }

        writer.WriteGeneratedAttributes(GeneratorName);
        writer.Write($"private {typeName}(");

        WriteArguments(
            writer,
            item.Members.AsImmutableArray().AsSpan(),
            static (member, writer) => writer.Write($"{member.TypeName} @{member.ParameterName}"));

        writer.WriteLine(")");

        using (writer.WriteBlock())
        {
            foreach (ResourceGroupMemberInfo member in item.Members)
            {
                writer.WriteLine($"this.@{member.PropertyName} = @{member.ParameterName};");
            }
        }
    }

    /// <summary>
    /// Writes the generation factory of a single resource group.
    /// </summary>
    /// <param name="item">The resource group to write the factory for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceGroupGenerationFactory(ResourceGroupPlanInfo item, IndentedTextWriter writer)
    {
        string typeName = item.Hierarchy.Hierarchy[0].QualifiedName;

        writer.WriteLine($"""/// <summary>Gets the <see cref="{typeName}"/> instance describing a pinned resource generation.</summary>""");
        writer.WriteLine("""/// <param name="generation">The instance describing the last pinned resource generation of the owning slot.</param>""");

        foreach (ResourceGroupMemberInfo member in item.Members)
        {
            writer.WriteLine($"""/// <param name="{member.ParameterName}">The pinned resource owned by <c>{member.PropertyName}</c>.</param>""");
        }

        writer.WriteLine($"""/// <returns>The <see cref="{typeName}"/> instance describing the pinned resource generation.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.Write($"internal static {typeName} {GeneratedIdentifier.ResourceGroupGenerationFactoryName}(ref {typeName}? generation");

        foreach (ResourceGroupMemberInfo member in item.Members)
        {
            writer.Write($", {member.TypeName} @{member.ParameterName}");
        }

        writer.WriteLine(")");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"{typeName}? current = generation;");
            writer.WriteLine();
            writer.WriteLine("if (current is not null &&");
            writer.IncreaseIndent();

            for (int i = 0; i < item.Members.Length; i++)
            {
                ResourceGroupMemberInfo member = item.Members[i];

                writer.Write($"global::System.Object.ReferenceEquals(current.@{member.PropertyName}, @{member.ParameterName})");
                writer.WriteLine(i < item.Members.Length - 1 ? " &&" : ")");
            }

            writer.DecreaseIndent();

            using (writer.WriteBlock())
            {
                writer.WriteLine("return current;");
            }

            writer.WriteLine();
            writer.Write($"{typeName} created = new(");

            WriteArguments(
                writer,
                item.Members.AsImmutableArray().AsSpan(),
                static (member, writer) => writer.Write($"@{member.ParameterName}"));

            writer.WriteLine(");");
            writer.WriteLine();
            writer.WriteLine("generation = created;");
            writer.WriteLine();
            writer.WriteLine("return created;");
        }
    }
}
