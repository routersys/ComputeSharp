using System.Threading;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Resources.Plans;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed class PipelineHostRuntime
{
    private readonly Lock registrationGate = new();

    private readonly IComputeOwnedSlot[] slots;

    private HostRegistrationRecord registration;

    private ulong nextPreparedToken;

    internal PipelineHostRuntime(
        GraphicsDevice device,
        in PipelineHostDescriptor descriptor,
        in HostRegistrationRecord registration,
        in HostStructuralReservation reservation,
        PipelineCommandListPartition commandLists,
        ResourceUsageSetPartition usageSets,
        PendingSubmissionRecordPartition pendingRecords,
        int[] planStorage,
        SlotResourcePlanStateRecord[] planStates,
        OwnedSlotResourceLayout[] slotLayouts,
        ComputeResourceAccess[] slotResourceAccesses,
        ComputeGenerationDeclaration[] slotResourceDeclarations,
        ResourceIdentityAllocator identities,
        IComputeOwnedSlot[] slots)
    {
        Device = device;
        Descriptor = descriptor;
        Reservation = reservation;
        CommandLists = commandLists;
        UsageSets = usageSets;
        PendingRecords = pendingRecords;
        PlanStorage = planStorage;
        PlanStates = planStates;
        SlotLayouts = slotLayouts;
        SlotResourceAccesses = slotResourceAccesses;
        SlotResourceDeclarations = slotResourceDeclarations;
        Identities = identities;

        this.registration = registration;
        this.slots = slots;
    }

    public HostRegistrationId Id => this.registration.Id;

    public GraphicsDevice Device { get; }

    public PipelineHostDescriptor Descriptor { get; }

    public HostStructuralReservation Reservation { get; }

    public PipelineCommandListPartition CommandLists { get; }

    public ResourceUsageSetPartition UsageSets { get; }

    public PendingSubmissionRecordPartition PendingRecords { get; }

    public int[] PlanStorage { get; }

    public SlotResourcePlanStateRecord[] PlanStates { get; }

    public OwnedSlotResourceLayout[] SlotLayouts { get; }

    public ComputeResourceAccess[] SlotResourceAccesses { get; }

    public ComputeGenerationDeclaration[] SlotResourceDeclarations { get; }

    public ResourceIdentityAllocator Identities { get; }

    public int SlotCount => this.slots.Length;

    public RegistrationState State
    {
        get
        {
            lock (this.registrationGate)
            {
                return this.registration.State;
            }
        }
    }

    public bool IsDisposalComplete
    {
        get
        {
            lock (this.registrationGate)
            {
                return this.registration.State is RegistrationState.Released;
            }
        }
    }

    public IComputeOwnedSlot GetSlot(int slotOrdinal)
    {
        return this.slots[slotOrdinal];
    }

    public ulong CreatePreparedToken()
    {
        return Interlocked.Increment(ref this.nextPreparedToken);
    }

    public bool TryCommitActive()
    {
        lock (this.registrationGate)
        {
            return this.registration.TryCommitActive();
        }
    }

    public bool TryAcquireInvocation()
    {
        lock (this.registrationGate)
        {
            return this.registration.TryAcquireInvocation();
        }
    }

    public void ReleaseInvocation()
    {
        lock (this.registrationGate)
        {
            this.registration.ReleaseInvocation();
        }
    }

    public bool TryCheckoutPendingRecord(PipelineKey pipeline, ulong submissionSequence, out int index)
    {
        lock (this.registrationGate)
        {
            if (!this.registration.TryReservePendingSubmission())
            {
                index = -1;

                return false;
            }
        }

        if (PendingRecords.TryCheckout(pipeline, submissionSequence, out index))
        {
            return true;
        }

        lock (this.registrationGate)
        {
            this.registration.ReleasePendingSubmission();
        }

        return false;
    }

    public void ReturnPendingRecord(int index)
    {
        PendingRecords.Return(index);

        lock (this.registrationGate)
        {
            this.registration.ReleasePendingSubmission();
        }
    }

    public UsageSetHandle GetUsageSetHandle(int pendingRecordIndex)
    {
        return UsageSets.GetHandle(pendingRecordIndex);
    }

    public void RequestDispose()
    {
        lock (this.registrationGate)
        {
            if (!this.registration.TryRequestDispose())
            {
                return;
            }
        }

        foreach (IComputeOwnedSlot slot in this.slots)
        {
            slot.RequestDispose();
        }
    }

    public bool TryBeginRelease()
    {
        bool isOwnedSlotDisposalComplete = true;

        foreach (IComputeOwnedSlot slot in this.slots)
        {
            if (!slot.IsDisposalComplete)
            {
                isOwnedSlotDisposalComplete = false;

                break;
            }
        }

        lock (this.registrationGate)
        {
            return this.registration.TryBeginRelease(isOwnedSlotDisposalComplete);
        }
    }

    public bool TryCompleteRelease()
    {
        lock (this.registrationGate)
        {
            return this.registration.TryCompleteRelease();
        }
    }
}
