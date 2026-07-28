using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators;

/// <inheritdoc/>
partial class PipelineDescriptorGenerator
{
    /// <summary>
    /// The name of the generated field holding the runtime of a compute interop resource set.
    /// </summary>
    private const string ResourceSetRuntimeFieldName = "computeInteropResourceSetRuntime";

    /// <summary>
    /// Writes the registration members of a given compute interop resource set.
    /// </summary>
    /// <param name="item">The resource set to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceSetRegistration(InteropResourceSetInfo item, IndentedTextWriter writer)
    {
        writer.WriteLine("/// <summary>The runtime the current resource set is registered on.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName, includeNonUserCodeAttributes: false);
        writer.WriteLine($"private readonly global::ComputeSharp.ComputeInteropResourceSetRuntime {ResourceSetRuntimeFieldName};");
        writer.WriteLine();

        writer.WriteLine($"""/// <summary>Creates a new <see cref="{item.TypeName}"/> instance with the specified parameters.</summary>""");
        writer.WriteLine("""/// <param name="device">The device to register the resource set on.</param>""");
        writer.WriteLine("""/// <param name="domain">The interop domain the resource set shares its textures with.</param>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"private {item.TypeName}(global::ComputeSharp.GraphicsDevice device, global::ComputeSharp.ComputeInteropDomain domain)");

        using (writer.WriteBlock())
        {
            foreach (SharedTextureSlotSyntaxInfo slot in item.Slots)
            {
                writer.WriteLine($"this.@{slot.FieldName} = new {slot.SlotTypeName}();");
            }

            writer.Write(
                $"this.{ResourceSetRuntimeFieldName} = " +
                "global::ComputeSharp.ComputeInteropResourceSetRuntime.Create(device, domain, CanonicalDescriptor, [");

            WriteArguments(writer, item.Slots.AsImmutableArray().AsSpan(), static (slot, writer) => writer.Write($"this.@{slot.FieldName}"));

            writer.WriteLine("]);");
        }

        writer.WriteLine();
        writer.WriteLine($"""/// <summary>Creates a new <see cref="{item.TypeName}"/> instance registered against an interop domain.</summary>""");
        writer.WriteLine("""/// <param name="device">The device to register the resource set on.</param>""");
        writer.WriteLine("""/// <param name="domain">The interop domain the resource set shares its textures with.</param>""");
        writer.WriteLine($"""/// <returns>The registered <see cref="{item.TypeName}"/> instance.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"public static {item.TypeName} Create(global::ComputeSharp.GraphicsDevice device, global::ComputeSharp.ComputeInteropDomain domain)");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return new {item.TypeName}(device, domain);");
        }

        writer.WriteLine();
        writer.WriteLine("/// <inheritdoc/>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine("public void Dispose()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"this.{ResourceSetRuntimeFieldName}.Dispose();");

            foreach (SharedTextureSlotSyntaxInfo slot in item.Slots)
            {
                writer.WriteLine($"this.@{slot.FieldName}.Dispose();");
            }
        }

        writer.WriteLine();
        writer.WriteLine("/// <summary>Waits for the disposal of the current resource set to complete.</summary>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine("public void WaitForDisposal()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"this.{ResourceSetRuntimeFieldName}.WaitForDisposal();");
        }
    }

    /// <summary>
    /// Writes the typed slot members of a given compute interop resource set.
    /// </summary>
    /// <param name="item">The resource set to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceSetSlots(InteropResourceSetInfo item, IndentedTextWriter writer)
    {
        writer.WriteLineSeparatedMembers(
            item.Slots.AsImmutableArray().AsSpan(),
            static (slot, writer) => WriteResourceSetSlot(slot, writer));
    }

    /// <summary>
    /// Writes the typed members of a single shared texture slot.
    /// </summary>
    /// <param name="slot">The shared texture slot to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteResourceSetSlot(SharedTextureSlotSyntaxInfo slot, IndentedTextWriter writer)
    {
        writer.WriteLine($"""/// <summary>Ensures the shared texture owned by <c>{slot.CanonicalName}</c> matches the requested logical dimensions.</summary>""");
        writer.WriteLine("""/// <param name="width">The requested logical width.</param>""");
        writer.WriteLine("""/// <param name="height">The requested logical height.</param>""");
        writer.WriteLine("""/// <param name="changed">Whether the published texture generation was replaced.</param>""");
        writer.WriteLine("""/// <returns>Whether the shared texture matches the requested logical dimensions.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine($"public bool TryEnsure{slot.CanonicalName}(int width, int height, out bool changed)");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return this.@{slot.FieldName}.TryEnsure(width, height, out changed);");
        }

        writer.WriteLine();
        writer.WriteLine($"""/// <summary>Gets the binding of the shared texture owned by <c>{slot.CanonicalName}</c>.</summary>""");
        writer.WriteLine("""/// <returns>The binding of the shared texture, or an invalid binding if none is published.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine(
            $"{slot.BindingAccessibility} global::ComputeSharp.ComputeResourceBinding<{slot.BindingTypeName}> " +
            $"Get{slot.CanonicalName}ComputeBinding()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return this.@{slot.FieldName}.GetComputeBinding();");
        }

        writer.WriteLine();
        writer.WriteLine($"""/// <summary>Begins a transient external operation over the shared texture owned by <c>{slot.CanonicalName}</c>.</summary>""");
        writer.WriteLine("""/// <returns>A transient borrow of the external view.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine(
            $"{slot.ViewAccessibility} global::ComputeSharp.BorrowedExternalTextureView<{slot.ViewTypeName}> " +
            $"Begin{slot.CanonicalName}ExternalOperation()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return this.@{slot.FieldName}.BeginExternalOperation();");
        }

        writer.WriteLine();
        writer.WriteLine($"""/// <summary>Acquires a persistent lease over the external view of the shared texture owned by <c>{slot.CanonicalName}</c>.</summary>""");
        writer.WriteLine("""/// <returns>A persistent lease over the external view.</returns>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.WriteLine(
            $"{slot.ViewAccessibility} global::ComputeSharp.ExternalTextureLease<{slot.ViewTypeName}> " +
            $"Acquire{slot.CanonicalName}ExternalViewLease()");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"return this.@{slot.FieldName}.AcquireExternalViewLease();");
        }
    }
}
