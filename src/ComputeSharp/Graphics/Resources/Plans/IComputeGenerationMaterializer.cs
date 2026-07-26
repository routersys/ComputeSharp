namespace ComputeSharp;

/// <summary>
/// A materializer declaring the resources of a single owned slot generation.
/// </summary>
public interface IComputeGenerationMaterializer
{
    /// <summary>
    /// Declares every resource of the generation being materialized.
    /// </summary>
    /// <param name="context">The context to declare the resources of the generation into.</param>
    void Materialize(ref ComputeGenerationContext context);
}
