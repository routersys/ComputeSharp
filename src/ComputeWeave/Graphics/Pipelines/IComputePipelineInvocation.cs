namespace ComputeWeave;

/// <summary>
/// An invocation of a generated compute pipeline method, holding its host and its transformed parameters.
/// </summary>
public interface IComputePipelineInvocation
{
    /// <summary>
    /// Gets the ordinal of the pipeline method being invoked, within its host descriptor.
    /// </summary>
    static abstract int PipelineOrdinal { get; }

    /// <summary>
    /// Pins every resource the invocation binds, in contract ordinal order.
    /// </summary>
    /// <param name="binder">The binder to pin the bound resources into.</param>
    void Bind(ref ComputePipelineBinder binder);

    /// <summary>
    /// Records the pipeline method into a context owned by the host.
    /// </summary>
    /// <param name="context">The context to record the pipeline method into.</param>
    void Record(in ComputeContext context);
}
