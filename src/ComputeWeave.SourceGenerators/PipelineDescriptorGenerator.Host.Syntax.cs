using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGenerators.Helpers;
using ComputeWeave.SourceGenerators.Models;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
partial class PipelineDescriptorGenerator
{
    /// <summary>
    /// The name of the generated field holding the runtime of a compute pipeline host.
    /// </summary>
    private const string RuntimeFieldName = "computeHostRuntime";

    /// <summary>
    /// Writes the registration members of a given compute pipeline host.
    /// </summary>
    /// <param name="item">The host to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteHostRegistration(PipelineHostInfo item, IndentedTextWriter writer)
    {
        writer.WriteLine("/// <summary>The runtime the current host is registered on.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName, includeNonUserCodeAttributes: false);
        writer.WriteLine($"private readonly global::ComputeWeave.ComputeHostRuntime {RuntimeFieldName};");
        writer.WriteLine();

        writer.WriteLine($"""/// <summary>Creates a new <see cref="{item.TypeName}"/> instance with the specified parameters.</summary>""");
        writer.WriteLine("""/// <param name="device">The device to register the host on.</param>""");
        writer.WriteLine("""/// <param name="maximumPendingSubmissions">The maximum number of pending submissions to reserve for the host.</param>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"private {item.TypeName}(global::ComputeWeave.GraphicsDevice device, int maximumPendingSubmissions)");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"this.@{item.DeviceFieldName} = device;");
            writer.Write($"this.{RuntimeFieldName} = global::ComputeWeave.ComputeHostRuntime.Create(device, CanonicalDescriptor, maximumPendingSubmissions, [");

            WriteArguments(writer, item.Slots.AsImmutableArray().AsSpan(), static (slot, writer) => writer.Write($"this.@{slot.FieldName}"));

            writer.WriteLine("]);");
        }

        writer.WriteLine();
        writer.WriteLine($"""/// <summary>Creates a new <see cref="{item.TypeName}"/> instance registered on a given device.</summary>""");
        writer.WriteLine("""/// <param name="device">The device to register the host on.</param>""");
        writer.WriteLine("""/// <param name="maximumPendingSubmissions">The maximum number of pending submissions to reserve for the host.</param>""");
        writer.WriteLine($"""/// <returns>The registered <see cref="{item.TypeName}"/> instance.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"public static {item.TypeName} Create(global::ComputeWeave.GraphicsDevice device, int maximumPendingSubmissions)");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return new {item.TypeName}(device, maximumPendingSubmissions);");
        }

        writer.WriteLine();
        writer.WriteLine("/// <inheritdoc/>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine("public void Dispose()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"this.{RuntimeFieldName}.Dispose();");

            foreach (OwnedSlotSyntaxInfo slot in item.Slots)
            {
                writer.WriteLine($"this.@{slot.FieldName}.Dispose();");
            }
        }

        writer.WriteLine();
        writer.WriteLine("/// <summary>Waits for the disposal of the current host to complete.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine("public void WaitForDisposal()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"this.{RuntimeFieldName}.WaitForDisposal();");
        }
    }

    /// <summary>
    /// Writes the typed slot members of a given compute pipeline host.
    /// </summary>
    /// <param name="item">The host to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteHostSlots(PipelineHostInfo item, IndentedTextWriter writer)
    {
        writer.WriteLineSeparatedMembers(
            item.Slots.AsImmutableArray().AsSpan(),
            static (slot, writer) => WriteHostSlot(slot, writer));
    }

    /// <summary>
    /// Writes the typed members of a single owned slot.
    /// </summary>
    /// <param name="slot">The owned slot to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteHostSlot(OwnedSlotSyntaxInfo slot, IndentedTextWriter writer)
    {
        if (slot.GroupTypeName is string groupTypeName)
        {
            writer.WriteLine($"""/// <summary>The resource group instance describing the last generation of <c>{slot.CanonicalName}</c> pinned by a pipeline.</summary>""");
            writer.WriteGeneratedAttributes(GeneratorName, includeNonUserCodeAttributes: false);
            writer.WriteLine($"private {groupTypeName}? @{GeneratedIdentifier.CreateGenerationFieldName(slot.CanonicalName)};");
            writer.WriteLine();
        }

        writer.WriteLine($"""/// <summary>Ensures the resources owned by <c>{slot.CanonicalName}</c> match a requested resource plan.</summary>""");
        writer.WriteLine("""/// <param name="plan">The requested resource plan.</param>""");
        writer.WriteLine("""/// <param name="changed">Whether a new resource generation was published.</param>""");
        writer.WriteLine("""/// <returns>Whether the owned resources match <paramref name="plan"/>.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"{slot.PlanAccessibility} bool TryEnsure{slot.CanonicalName}(in {slot.PlanTypeName} plan, out bool changed)");

        using (writer.WriteBlock())
        {
            writer.Write($"return this.{RuntimeFieldName}.TryEnsureResource({slot.Ordinal}, [");

            WriteArguments(writer, slot.PlanFields.AsImmutableArray().AsSpan(), static (planField, writer) => writer.Write($"plan.{planField.PropertyName}"));

            writer.Write($"], new {slot.MaterializerTypeName}(");

            WriteArguments(writer, slot.PlanFields.AsImmutableArray().AsSpan(), static (planField, writer) => writer.Write($"plan.{planField.PropertyName}"));

            writer.WriteLine("), out changed);");
        }

        if (slot.BindingResourceTypeName is string bindingResourceTypeName)
        {
            writer.WriteLine();
            writer.WriteLine($"""/// <summary>Gets the binding of the resource owned by <c>{slot.CanonicalName}</c>.</summary>""");
            writer.WriteLine("""/// <returns>The binding of the owned resource, or an invalid binding if none is published.</returns>""");
            writer.WriteGeneratedAttributes(GeneratorName);
            writer.WriteLine($"{slot.BindingAccessibility} global::ComputeWeave.ComputeResourceBinding<{bindingResourceTypeName}> Get{slot.CanonicalName}ComputeBinding()");

            using (writer.WriteBlock())
            {
                writer.WriteLine($"return this.{RuntimeFieldName}.GetBinding<{bindingResourceTypeName}>({slot.Ordinal}, 0);");
            }
        }

        writer.WriteLine();
        WriteSlotMaterializer(slot, writer);
    }

    /// <summary>
    /// Writes the materializer type of a single owned slot.
    /// </summary>
    /// <param name="slot">The owned slot to write the materializer for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteSlotMaterializer(OwnedSlotSyntaxInfo slot, IndentedTextWriter writer)
    {
        writer.WriteLine($"""/// <summary>The materializer of the resources owned by <c>{slot.CanonicalName}</c>.</summary>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"private readonly struct {slot.MaterializerTypeName} : global::ComputeWeave.IComputeGenerationMaterializer");

        using (writer.WriteBlock())
        {
            foreach (ResourcePlanFieldInfo planField in slot.PlanFields)
            {
                writer.WriteLine($"""/// <summary>The requested <c>{planField.PropertyName}</c> dimension.</summary>""");
                writer.WriteLine($"private readonly int {planField.ParameterName};");
                writer.WriteLine();
            }

            writer.WriteLine($"""/// <summary>Creates a new <see cref="{slot.MaterializerTypeName}"/> instance with the specified parameters.</summary>""");

            foreach (ResourcePlanFieldInfo planField in slot.PlanFields)
            {
                writer.WriteLine($"""/// <param name="{planField.ParameterName}">The requested <c>{planField.PropertyName}</c> dimension.</param>""");
            }

            writer.Write($"public {slot.MaterializerTypeName}(");

            WriteArguments(writer, slot.PlanFields.AsImmutableArray().AsSpan(), static (planField, writer) => writer.Write($"int {planField.ParameterName}"));

            writer.WriteLine(")");

            using (writer.WriteBlock())
            {
                foreach (ResourcePlanFieldInfo planField in slot.PlanFields)
                {
                    writer.WriteLine($"this.{planField.ParameterName} = {planField.ParameterName};");
                }
            }

            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine($"public static bool RequiresDoublePrecisionSupport => {(slot.RequiresDoublePrecisionSupport ? "true" : "false")};");
            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public void Materialize(ref global::ComputeWeave.ComputeGenerationContext context)");

            using (writer.WriteBlock())
            {
                foreach (SlotResourceSyntaxInfo resource in slot.Resources)
                {
                    WriteResourceDeclaration(resource, writer);
                }
            }
        }
    }

    /// <summary>
    /// Writes the declaration of a single owned resource into a materializer.
    /// </summary>
    /// <param name="resource">The owned resource to write the declaration for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceDeclaration(SlotResourceSyntaxInfo resource, IndentedTextWriter writer)
    {
        writer.Write(resource.Shape is ResourcePlanKind.Buffer ? "context.DeclareBuffer<" : "context.DeclareTexture2D<");
        writer.Write(resource.ElementTypeName);

        if (resource.PixelTypeName is string pixelTypeName)
        {
            writer.Write(", ");
            writer.Write(pixelTypeName);
        }

        writer.Write(">(");

        WriteArguments(writer, resource.DimensionParameterNames.AsImmutableArray().AsSpan(), static (parameterName, writer) => writer.Write($"this.{parameterName}"));

        writer.WriteLine(");");
    }

    /// <summary>
    /// Writes a series of arguments separated by a comma on a single line.
    /// </summary>
    /// <typeparam name="T">The type of items to write.</typeparam>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    /// <param name="items">The items to write.</param>
    /// <param name="callback">The callback writing a single item.</param>
    private static void WriteArguments<T>(IndentedTextWriter writer, ReadOnlySpan<T> items, IndentedTextWriter.Callback<T> callback)
    {
        for (int i = 0; i < items.Length; i++)
        {
            writer.WriteIf(i > 0, ", ");

            callback(items[i], writer);
        }
    }
}
