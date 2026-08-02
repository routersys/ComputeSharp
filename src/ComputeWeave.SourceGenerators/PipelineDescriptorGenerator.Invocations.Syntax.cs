using ComputeWeave.SourceGeneration.Extensions;
using ComputeWeave.SourceGeneration.Helpers;
using ComputeWeave.SourceGenerators.Helpers;
using ComputeWeave.SourceGenerators.Models;

namespace ComputeWeave.SourceGenerators;

/// <inheritdoc/>
partial class PipelineDescriptorGenerator
{
    /// <summary>
    /// The suffix of the generated field holding the resource an external binding parameter was pinned to.
    /// </summary>
    private const string BoundResourceFieldSuffix = "BoundResource";

    /// <summary>
    /// Writes the invocation members of every pipeline of a given compute pipeline host.
    /// </summary>
    /// <param name="item">The host to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteHostInvocations(PipelineHostInfo item, IndentedTextWriter writer)
    {
        bool isFirst = true;

        foreach (PipelineInvocationSyntaxInfo invocation in item.Invocations)
        {
            if (!isFirst)
            {
                writer.WriteLine();
            }

            isFirst = false;

            WriteHostInvocation(item.TypeName, invocation, writer);
        }
    }

    /// <summary>
    /// Writes the invocation members of a single pipeline.
    /// </summary>
    /// <param name="hostTypeName">The name of the host declaring the pipeline.</param>
    /// <param name="invocation">The pipeline to write the members for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteHostInvocation(string hostTypeName, PipelineInvocationSyntaxInfo invocation, IndentedTextWriter writer)
    {
        writer.WriteLine($"""/// <summary>Records and submits a single invocation of <c>{invocation.MethodName}</c>.</summary>""");

        foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
        {
            writer.WriteLine($"""/// <param name="{parameter.ParameterName}">The <c>{parameter.ParameterName}</c> argument of the pipeline.</param>""");
        }

        writer.WriteLine("/// <returns>The value tracking the completion of the submitted work.</returns>");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.Write($"{invocation.Accessibility} global::ComputeWeave.ComputeSubmission {invocation.MethodName}(");

        WriteArguments(writer, invocation.Parameters.AsImmutableArray().AsSpan(), static (parameter, writer) =>
        {
            writer.WriteIf(parameter.IsReadOnlyReference, "in ");
            writer.Write($"{parameter.TypeName} @{parameter.ParameterName}");
        });

        writer.WriteLine(")");

        using (writer.WriteBlock())
        {
            writer.Write($"return this.{RuntimeFieldName}.Submit(new {invocation.InvocationTypeName}(this");

            foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
            {
                writer.Write($", @{parameter.ParameterName}");
            }

            writer.WriteLine("));");
        }

        writer.WriteLine();
        WriteInvocationType(hostTypeName, invocation, writer);
    }

    /// <summary>
    /// Writes the invocation type of a single pipeline.
    /// </summary>
    /// <param name="hostTypeName">The name of the host declaring the pipeline.</param>
    /// <param name="invocation">The pipeline to write the invocation type for.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteInvocationType(string hostTypeName, PipelineInvocationSyntaxInfo invocation, IndentedTextWriter writer)
    {
        writer.WriteLine($"""/// <summary>The invocation of <c>{invocation.MethodName}</c>.</summary>""");
        writer.WriteGeneratedAttributes(GeneratorName);
        writer.Write("private ");
        writer.WriteIf(!HasResolvedResources(invocation), "readonly ");
        writer.WriteLine($"struct {invocation.InvocationTypeName} : global::ComputeWeave.IComputePipelineInvocation");

        using (writer.WriteBlock())
        {
            writer.WriteLine("/// <summary>The host declaring the pipeline.</summary>");
            writer.WriteLine($"private readonly {hostTypeName} host;");
            writer.WriteLine();

            foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
            {
                writer.WriteLine($"""/// <summary>The <c>{parameter.ParameterName}</c> argument of the pipeline.</summary>""");
                writer.WriteLine($"private readonly {parameter.TypeName} @{parameter.ParameterName};");
                writer.WriteLine();

                if (parameter.BoundResourceTypeName is string boundResourceTypeName)
                {
                    writer.WriteLine($"""/// <summary>The resource the <c>{parameter.ParameterName}</c> binding was pinned to.</summary>""");
                    writer.WriteLine($"private {boundResourceTypeName} @{parameter.ParameterName}{BoundResourceFieldSuffix};");
                    writer.WriteLine();
                }
            }

            foreach (PipelineOwnedResourceSyntaxInfo owned in invocation.OwnedResources)
            {
                writer.WriteLine($"""/// <summary>The owned resources the <c>{owned.ParameterName}</c> argument of the pipeline was pinned to.</summary>""");
                writer.WriteLine($"private {owned.TypeName} @{owned.ParameterName};");
                writer.WriteLine();
            }

            writer.WriteLine($"""/// <summary>Creates a new <see cref="{invocation.InvocationTypeName}"/> instance with the specified parameters.</summary>""");
            writer.WriteLine("""/// <param name="host">The host declaring the pipeline.</param>""");

            foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
            {
                writer.WriteLine($"""/// <param name="{parameter.ParameterName}">The <c>{parameter.ParameterName}</c> argument of the pipeline.</param>""");
            }

            writer.Write($"public {invocation.InvocationTypeName}({hostTypeName} host");

            foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
            {
                writer.Write($", {parameter.TypeName} @{parameter.ParameterName}");
            }

            writer.WriteLine(")");

            using (writer.WriteBlock())
            {
                writer.WriteLine("this.host = host;");

                foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
                {
                    writer.WriteLine($"this.@{parameter.ParameterName} = @{parameter.ParameterName};");
                    writer.WriteLineIf(
                        parameter.BoundResourceTypeName is not null,
                        $"this.@{parameter.ParameterName}{BoundResourceFieldSuffix} = null!;");
                }

                foreach (PipelineOwnedResourceSyntaxInfo owned in invocation.OwnedResources)
                {
                    writer.WriteLine($"this.@{owned.ParameterName} = null!;");
                }
            }

            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine($"public static int PipelineOrdinal => {invocation.Ordinal};");
            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public void Bind(ref global::ComputeWeave.ComputePipelineBinder binder)");

            using (writer.WriteBlock())
            {
                for (int i = 0; i < invocation.Bindings.Length; i++)
                {
                    writer.WriteLineIf(i > 0);

                    WriteBinding(i, invocation.Bindings[i], writer);
                }

                foreach (PipelineOwnedResourceSyntaxInfo owned in invocation.OwnedResources)
                {
                    writer.WriteLine();
                    WriteOwnedResource(owned, writer);
                }
            }

            writer.WriteLine();
            writer.WriteLine("/// <inheritdoc/>");
            writer.WriteLine("public void Record(in global::ComputeWeave.ComputeContext context)");

            using (writer.WriteBlock())
            {
                writer.Write($"this.host.{invocation.MethodName}(in context");

                foreach (PipelineArgumentSyntaxInfo argument in invocation.Arguments)
                {
                    writer.Write(argument.IsReadOnlyReference ? ", in " : ", ");
                    writer.Write($"this.@{argument.Name}");
                    writer.WriteIf(argument.Kind is PipelineArgumentKind.ExternalResource, BoundResourceFieldSuffix);
                }

                writer.WriteLine(");");
            }
        }
    }

    /// <summary>
    /// Checks whether a pipeline resolves any pinned resource into the invocation.
    /// </summary>
    /// <param name="invocation">The pipeline to check the parameters of.</param>
    /// <returns>Whether <paramref name="invocation"/> resolves any pinned resource.</returns>
    private static bool HasResolvedResources(PipelineInvocationSyntaxInfo invocation)
    {
        if (!invocation.OwnedResources.IsEmpty)
        {
            return true;
        }

        foreach (PipelineParameterSyntaxInfo parameter in invocation.Parameters)
        {
            if (parameter.BoundResourceTypeName is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the resolution of a single owned resource parameter of a pipeline invocation.
    /// </summary>
    /// <param name="owned">The owned resource parameter to write the resolution of.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteOwnedResource(PipelineOwnedResourceSyntaxInfo owned, IndentedTextWriter writer)
    {
        if (owned.GenerationFieldName is not string generationFieldName)
        {
            writer.WriteLine($"this.@{owned.ParameterName} = resource{owned.BindingIndices[0]};");

            return;
        }

        writer.Write(
            $"this.@{owned.ParameterName} = {owned.TypeName}.{GeneratedIdentifier.ResourceGroupGenerationFactoryName}(" +
            $"ref this.host.@{generationFieldName}");

        foreach (int index in owned.BindingIndices)
        {
            writer.Write($", resource{index}");
        }

        writer.WriteLine(");");
    }

    /// <summary>
    /// Writes a single pin of a pipeline invocation.
    /// </summary>
    /// <param name="index">The contract ordinal of the pin.</param>
    /// <param name="binding">The pin to write.</param>
    /// <param name="writer">The target <see cref="IndentedTextWriter"/> instance.</param>
    private static void WriteBinding(int index, PipelineBindingSyntaxInfo binding, IndentedTextWriter writer)
    {
        string expression;

        if (binding.Kind is PipelineBindingKind.OwnedSlot)
        {
            writer.WriteLine(
                $"global::ComputeWeave.ComputeResourceBinding<{binding.ResourceTypeName}> binding{index} = " +
                $"this.host.{RuntimeFieldName}.GetBinding<{binding.ResourceTypeName}>({binding.SlotOrdinal}, {binding.SlotResourceIndex});");
            writer.WriteLine();

            expression = binding.IsResolved
                ? $"binder.TryPin({binding.SlotOrdinal}, in binding{index}, out {binding.ResourceTypeName} resource{index})"
                : $"binder.TryPin({binding.SlotOrdinal}, in binding{index})";
        }
        else if (binding.Kind is PipelineBindingKind.ExternalParameter)
        {
            expression = $"binder.TryPin(in this.@{binding.Name}, out this.@{binding.Name}{BoundResourceFieldSuffix})";
        }
        else if (binding.Kind is PipelineBindingKind.Parameter)
        {
            expression = $"binder.TryPin(this.@{binding.Name})";
        }
        else
        {
            expression = $"binder.TryPin(this.host.@{binding.Name})";
        }

        writer.WriteLine($"if (!{expression})");

        using (writer.WriteBlock())
        {
            writer.WriteLine($"""throw new global::System.InvalidOperationException("The resource \"{binding.Name}\" could not be pinned for the pipeline invocation.");""");
        }
    }
}
