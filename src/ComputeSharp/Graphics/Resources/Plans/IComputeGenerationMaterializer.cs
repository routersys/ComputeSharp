namespace ComputeSharp;

/// <summary>
/// A materializer declaring the resources of a single owned slot generation.
/// </summary>
public interface IComputeGenerationMaterializer
{
    /// <summary>
    /// Gets whether any resource declared by the current materializer stores double precision floating point numbers.
    /// </summary>
    /// <remarks>
    /// The element types of the declared resources are only known when the materializer is written, and the runtime
    /// cannot inspect them without reflection. Reporting them here allows the device capability they require to be
    /// validated before any resource of the generation is created.
    /// </remarks>
    static abstract bool RequiresDoublePrecisionSupport { get; }

    /// <summary>
    /// Declares every resource of the generation being materialized.
    /// </summary>
    /// <param name="context">The context to declare the resources of the generation into.</param>
    void Materialize(ref ComputeGenerationContext context);
}
