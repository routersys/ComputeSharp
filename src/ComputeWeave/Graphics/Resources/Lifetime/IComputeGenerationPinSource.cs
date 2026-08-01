using ComputeWeave.Graphics.Pipelines;

namespace ComputeWeave.Resources.Lifetime;

internal interface IComputeGenerationPinSource
{
    bool TryPinGeneration(
        ResourceGenerationSetId setId,
        ResourceGenerationId generationId,
        ulong bindingEpoch,
        int resourceIndex,
        out ResourceGenerationPin pin);
}
