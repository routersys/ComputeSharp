using System.Threading;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed class InteropResourceSetRuntime
{
    private readonly Lock registrationGate = new();

    private readonly DeviceRegistrationRegistry registry;

    private readonly IComputeSharedSlot[] slots;

    private ResourceSetRegistrationRecord registration;

    private ulong nextPreparedToken;

    internal InteropResourceSetRuntime(
        DeviceRegistrationRegistry registry,
        GraphicsDevice device,
        ComputeInteropDomain domain,
        in InteropResourceSetDescriptor descriptor,
        in ResourceSetRegistrationRecord registration,
        in ResourceSetStructuralReservation reservation,
        int[] planStorage,
        SlotResourcePlanStateRecord[] planStates,
        IComputeSharedSlot[] slots)
    {
        Device = device;
        Domain = domain;
        Descriptor = descriptor;
        Reservation = reservation;
        PlanStorage = planStorage;
        PlanStates = planStates;

        this.registry = registry;
        this.registration = registration;
        this.slots = slots;
    }

    public ResourceSetRegistrationId Id => this.registration.Id;

    public DeviceRegistrationRegistry Registry => this.registry;

    public GraphicsDevice Device { get; }

    public ComputeInteropDomain Domain { get; }

    public InteropResourceSetDescriptor Descriptor { get; }

    public ResourceSetStructuralReservation Reservation { get; }

    public int[] PlanStorage { get; }

    public SlotResourcePlanStateRecord[] PlanStates { get; }

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

    public IComputeSharedSlot GetSlot(int slotOrdinal)
    {
        return this.slots[slotOrdinal];
    }

    public ulong CreatePreparedToken()
    {
        ulong value = Interlocked.Increment(ref this.nextPreparedToken);

        if (value == 0)
        {
            Device.ThrowTerminalSequenceExhaustion("prepared replacement token");
        }

        return value;
    }

    public bool TryCommitActive()
    {
        lock (this.registrationGate)
        {
            return this.registration.TryCommitActive();
        }
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

        foreach (IComputeSharedSlot slot in this.slots)
        {
            slot.RequestDispose();
        }
    }

    public void RunSharedSlotMaintenance()
    {
        foreach (IComputeSharedSlot slot in this.slots)
        {
            slot.RunMaintenance();
        }
    }

    public bool TryGetMinimumDrainFence(out ulong fenceValue)
    {
        fenceValue = 0;

        bool hasDrainFence = false;

        foreach (IComputeSharedSlot slot in this.slots)
        {
            if (!slot.TryGetPendingDrainFence(out FencePoint fence) || fence.Queue is not ComputeQueueKind.Compute)
            {
                continue;
            }

            if (!hasDrainFence || fence.Value < fenceValue)
            {
                fenceValue = fence.Value;
            }

            hasDrainFence = true;
        }

        return hasDrainFence;
    }

    public void MarkSharedSlotsTerminalRetained()
    {
        foreach (IComputeSharedSlot slot in this.slots)
        {
            slot.MarkTerminalRetained();
        }
    }

    public void ReleaseSharedSlotTerminalGenerations()
    {
        foreach (IComputeSharedSlot slot in this.slots)
        {
            slot.ReleaseTerminalGenerations();
        }
    }

    public bool TryCompleteDeferredRelease()
    {
        if (State is RegistrationState.DisposeRequested)
        {
            _ = this.registry.TryUnregisterResourceSet(this);
        }

        return IsDisposalComplete;
    }

    public bool TryBeginRelease()
    {
        bool isSharedSlotDisposalComplete = true;

        foreach (IComputeSharedSlot slot in this.slots)
        {
            if (!slot.IsDisposalComplete)
            {
                isSharedSlotDisposalComplete = false;

                break;
            }
        }

        lock (this.registrationGate)
        {
            return this.registration.TryBeginRelease(isSharedSlotDisposalComplete);
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
