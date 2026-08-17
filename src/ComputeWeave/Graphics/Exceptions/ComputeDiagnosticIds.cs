using ComputeWeave.Interop;

namespace ComputeWeave;

/// <summary>
/// The identifiers of the runtime diagnostics, as declared by the normative specification.
/// </summary>
/// <remarks>
/// The numbers are grouped by band: 1000 for invocation and lifetime, 2000 for aliasing and contract, 3000 for
/// interop and scheduling, 4000 for resource state and generations, 5000 for the device and completion, and
/// 6000 for memory. An identifier is never reused for another meaning once it ships.
/// </remarks>
internal static class ComputeDiagnosticIds
{
    /// <summary>A resource of another device was bound.</summary>
    public const string DeviceMismatch = "CMPW1001";

    /// <summary>Disposal of the target was already requested.</summary>
    public const string DisposeRequested = "CMPW1002";

    /// <summary>The generation or binding epoch is stale.</summary>
    public const string StaleGeneration = "CMPW1003";

    /// <summary>The concurrent invocation limit is reached.</summary>
    public const string InvocationLimit = "CMPW1004";

    /// <summary>The pending record limit is reached.</summary>
    public const string PendingRecordLimit = "CMPW1005";

    /// <summary>The requested aliasing is rejected.</summary>
    public const string AliasingRejected = "CMPW2001";

    /// <summary>The observed access exceeds the declared contract.</summary>
    public const string AccessExceedsContract = "CMPW2002";

    /// <summary>The resource belongs to another interop domain.</summary>
    public const string DomainMismatch = "CMPW3001";

    /// <summary>The domain is poisoned, or its teardown has started.</summary>
    public const string DomainUnusable = "CMPW3003";

    /// <summary>A domain operation is already active.</summary>
    public const string DomainOperationActive = "CMPW3004";

    /// <summary>The scheduler violated its contract.</summary>
    public const string SchedulerContract = "CMPW3005";

    /// <summary>The timeline of the domain is exhausted.</summary>
    public const string DomainTimelineExhausted = "CMPW3006";

    /// <summary>The scheduler is busy, or was reentered.</summary>
    public const string SchedulerBusy = "CMPW3007";

    /// <summary>The resource state is unknown.</summary>
    public const string UnknownResourceState = "CMPW4001";

    /// <summary>The requested generation is unavailable.</summary>
    public const string GenerationUnavailable = "CMPW4002";

    /// <summary>Execution was issued without proof of completion.</summary>
    public const string CompletionProofMissing = "CMPW5001";

    /// <summary>The device reached its terminal state.</summary>
    public const string DeviceTerminal = "CMPW5002";

    /// <summary>A pending record invariant does not hold.</summary>
    public const string PendingRecordInvariant = "CMPW5003";

    /// <summary>The device sequence is exhausted.</summary>
    public const string DeviceSequenceExhausted = "CMPW5004";

    /// <summary>A completion publish invariant does not hold.</summary>
    public const string CompletionPublishInvariant = "CMPW5005";

    /// <summary>The DXGI budget is unavailable.</summary>
    public const string BudgetUnavailable = "CMPW6001";

    /// <summary>The configured broker granted nothing.</summary>
    public const string BrokerGrantUnavailable = "CMPW6002";

    /// <summary>The allocation descriptor is invalid.</summary>
    public const string AllocationDescriptorInvalid = "CMPW6003";

    /// <summary>The policy being replaced is already retired.</summary>
    public const string PolicyAlreadyRetired = "CMPW6004";

    /// <summary>A checked capacity computation overflowed.</summary>
    public const string CapacityOverflow = "CMPW6005";

    /// <summary>
    /// Maps the outcome of a domain operation acquisition to the diagnostic that rejects it.
    /// </summary>
    /// <param name="status">The outcome to map.</param>
    /// <returns>The identifier of the diagnostic matching <paramref name="status"/>.</returns>
    public static string FromDomainOperationStatus(DomainOperationStatus status)
    {
        return status switch
        {
            DomainOperationStatus.DomainUnavailable => DomainUnusable,
            DomainOperationStatus.TokenExhausted => DomainTimelineExhausted,
            DomainOperationStatus.PermitBusy => DomainOperationActive,
            _ => SchedulerBusy
        };
    }
}
