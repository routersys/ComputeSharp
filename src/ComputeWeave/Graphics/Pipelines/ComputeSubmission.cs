namespace ComputeWeave;

/// <summary>
/// A handle to the completion of a single compute submission.
/// </summary>
/// <remarks>
/// The default value represents a completed no-op. Instances only hold the device the submission was issued
/// on and the fence point it completes at, so they stay valid after the pending record backing the submission
/// has been returned to its pool.
/// </remarks>
public readonly struct ComputeSubmission
{
    /// <summary>
    /// The device the submission was issued on, or <see langword="null"/> for a completed no-op.
    /// </summary>
    private readonly GraphicsDevice? device;

    /// <summary>
    /// Creates a new <see cref="ComputeSubmission"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The device the submission was issued on.</param>
    /// <param name="completion">The fence point the submission completes at.</param>
    internal ComputeSubmission(GraphicsDevice device, FencePoint completion)
    {
        this.device = device;
        Completion = completion;
    }

    /// <summary>
    /// Gets the fence point the submission completes at.
    /// </summary>
    public FencePoint Completion { get; }

    /// <summary>
    /// Gets the outcome of the submission.
    /// </summary>
    public ComputeSubmissionStatus Status
    {
        get
        {
            if (this.device is not GraphicsDevice device || Completion.IsNone)
            {
                return ComputeSubmissionStatus.Succeeded;
            }

            return device.GetSubmissionStatus(Completion);
        }
    }

    /// <summary>
    /// Gets whether the submission reached its outcome.
    /// </summary>
    public bool IsCompleted => Status is not ComputeSubmissionStatus.Pending;

    /// <summary>
    /// Waits for the submission to reach its outcome.
    /// </summary>
    public void Wait()
    {
        if (this.device is not GraphicsDevice device || Completion.IsNone)
        {
            return;
        }

        device.WaitForSubmission(Completion);
    }
}
