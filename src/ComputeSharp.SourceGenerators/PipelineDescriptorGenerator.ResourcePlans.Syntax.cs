using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators;

/// <inheritdoc/>
partial class PipelineDescriptorGenerator
{
    /// <summary>
    /// Writes a single exact resource plan type.
    /// </summary>
    /// <param name="plan">The resource plan to write the type for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourcePlan(ResourcePlanInfo plan, IndentedTextWriter writer)
    {
        writer.WriteLine("/// <summary>An exact resource plan describing the dimensions of an owned resource.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"public readonly struct {plan.TypeName}");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"""/// <summary>Creates a new <see cref="{plan.TypeName}"/> instance with the specified parameters.</summary>""");

            foreach (ResourcePlanFieldInfo field in plan.Fields)
            {
                writer.WriteLine($"""/// <param name="{field.ParameterName}">The value of <see cref="{field.PropertyName}"/>.</param>""");
            }

            writer.Write($"public {plan.TypeName}(");

            for (int i = 0; i < plan.Fields.Length; i++)
            {
                writer.WriteIf(i > 0, ", ");
                writer.Write($"int {plan.Fields[i].ParameterName}");
            }

            writer.WriteLine(")");

            using (writer.WriteBlock())
            {
                foreach (ResourcePlanFieldInfo field in plan.Fields)
                {
                    writer.WriteLine($"{field.PropertyName} = {field.ParameterName};");
                }
            }

            foreach (ResourcePlanFieldInfo field in plan.Fields)
            {
                writer.WriteLine();
                writer.WriteLine($"/// <summary>Gets the <c>{field.PropertyName}</c> dimension of the plan.</summary>");
                writer.WriteLine($"public int {field.PropertyName} {{ get; }}");
            }
        }
    }
}
