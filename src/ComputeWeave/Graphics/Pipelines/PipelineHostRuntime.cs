using System.Threading;
using ComputeWeave.Graphics.Commands.Interop;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Resources.Plans;

namespace ComputeWeave.Graphics.Pipelines;

internal sealed class PipelineHostRuntime
{
    private readonly Lock registrationGate = new();

    private readonly DeviceRegistrationRegistry registry;

    private readonly IComputeOwnedSlot[] slots;

    private HostRegistrationRecord registration;

    private ulong nextPreparedToken;

    private ulong nextSubmissionSequence;

    internal PipelineHostRuntime(
        DeviceRegistrationRegistry registry,
        GraphicsDevice device,
        in PipelineHostDescriptor descriptor,
        in HostRegistrationRecord registration,
        in HostStructuralReservation reservation,
        PipelineCommandListPartition commandLists,
        ResourceUsageSetPartition usageSets,
        PendingSubmissionRecordPartition pendingRecords,
        RecordingBundlePartition recordingBundles,
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
        RecordingBundles = recordingBundles;
        PlanStorage = planStorage;
        PlanStates = planStates;
        SlotLayouts = slotLayouts;
        SlotResourceAccesses = slotResourceAccesses;
        SlotResourceDeclarations = slotResourceDeclarations;
        Identities = identities;

        this.registry = registry;
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

    public RecordingBundlePartition RecordingBundles { get; }

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
        return CreateSequenceValue(ref this.nextPreparedToken, "prepared replacement token");
    }

    public ulong CreateSubmissionSequence()
    {
        return CreateSequenceValue(ref this.nextSubmissionSequence, "submission sequence");
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
        bool isDisposeRequested;

        lock (this.registrationGate)
        {
            this.registration.ReleaseInvocation();

            isDisposeRequested = this.registration.State is RegistrationState.DisposeRequested;
        }

        if (isDisposeRequested)
        {
            this.registry.Coordinator.Wake();
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

    public ResourceUsageRecorder CreateUsageRecorder(int pendingRecordIndex)
    {
        return new ResourceUsageRecorder(UsageSets, GetUsageSetHandle(pendingRecordIndex));
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

    public void RunOwnedSlotMaintenance()
    {
        foreach (IComputeOwnedSlot slot in this.slots)
        {
            slot.RunMaintenance();
        }
    }

    public void MarkOwnedSlotsTerminalRetained()
    {
        foreach (IComputeOwnedSlot slot in this.slots)
        {
            slot.MarkTerminalRetained();
        }
    }

    public void ReleaseOwnedSlotTerminalGenerations()
    {
        foreach (IComputeOwnedSlot slot in this.slots)
        {
            slot.ReleaseTerminalGenerations();
        }
    }

    public bool TryCompleteDeferredRelease()
    {
        if (State is RegistrationState.DisposeRequested)
        {
            _ = this.registry.TryUnregisterHost(this);
        }

        return IsDisposalComplete;
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

    private ulong CreateSequenceValue(ref ulong sequence, string name)
    {
        ulong value = Interlocked.Increment(ref sequence);

        if (value == 0)
        {
            Device.ThrowTerminalSequenceExhaustion(name);
        }

        return value;
    }
}
