using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGenerators.Models;

namespace ComputeSharp.SourceGenerators.Helpers;

/// <summary>
/// The derivation rules for the structural requirements of a pipeline contract model.
/// </summary>
internal static class PipelineStructuralRequirements
{
    /// <summary>
    /// Derives the structural requirements of a single pipeline from its resource contracts.
    /// </summary>
    /// <param name="pipeline">The pipeline to derive the structural requirements for.</param>
    /// <returns>The pipeline, with its structural requirements assigned.</returns>
    public static PipelineContractInfo Derive(PipelineContractInfo pipeline)
    {
        int trackedResourceCount = checked(pipeline.Parameters.Length + pipeline.InternalResources.Length);
        int commandListSegments = 1;

        if (trackedResourceCount > 0)
        {
            commandListSegments++;
        }

        if ((pipeline.Flags & PipelineFlags.InteropRoundTrip) != 0)
        {
            commandListSegments++;
        }

        return pipeline with
        {
            MaximumTrackedResourceCount = trackedResourceCount,
            MaximumCommandListSegments = commandListSegments
        };
    }

    /// <summary>
    /// Derives the static structural requirements of a pipeline host from its pipelines and owned slots.
    /// </summary>
    /// <param name="pipelines">The pipelines of the host, with their structural requirements already assigned.</param>
    /// <param name="ownedSlotCount">The number of owned slots of the host.</param>
    /// <returns>The static structural requirements of the host.</returns>
    public static StructuralRequirementsInfo Derive(EquatableArray<PipelineContractInfo> pipelines, int ownedSlotCount)
    {
        int maximumTrackedResourceCount = 0;
        int maximumCommandListSegments = 0;

        foreach (PipelineContractInfo pipeline in pipelines)
        {
            if (pipeline.MaximumTrackedResourceCount > maximumTrackedResourceCount)
            {
                maximumTrackedResourceCount = pipeline.MaximumTrackedResourceCount;
            }

            if (pipeline.MaximumCommandListSegments > maximumCommandListSegments)
            {
                maximumCommandListSegments = pipeline.MaximumCommandListSegments;
            }
        }

        return new StructuralRequirementsInfo(maximumTrackedResourceCount, maximumCommandListSegments, ownedSlotCount);
    }
}
