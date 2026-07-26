using System;
using System.Collections.Generic;
using System.Threading;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed unsafe class DeviceRegistrationRegistry : IDisposable
{
    private readonly GraphicsDevice device;

    private readonly PipelineCommandListPool commandListPool;

    private readonly ResourceUsageSetPool usageSetPool = new();

    private readonly List<PipelineHostRuntime> hosts = [];

    private readonly Lock registrationGate = new();

    private DeviceStructuralAggregate aggregate;

    private ulong nextHostRegistrationId;

    private bool isDisposed;

    public DeviceRegistrationRegistry(GraphicsDevice device, D3D12_COMMAND_LIST_TYPE d3D12CommandListType)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(NotSupportedException).ThrowIf(device.HasOpaqueMemoryAllocator);

        this.device = device;
        this.commandListPool = new PipelineCommandListPool(device.D3D12Device, d3D12CommandListType);
    }

    public GraphicsDevice Device => this.device;

    public int HostCount
    {
        get
        {
            lock (this.registrationGate)
            {
                return this.hosts.Count;
            }
        }
    }

    public DeviceStructuralAggregate Aggregate
    {
        get
        {
            lock (this.registrationGate)
            {
                return this.aggregate;
            }
        }
    }

    public PipelineHostRuntime RegisterHost(
        ReadOnlySpan<byte> descriptor,
        int maximumPendingSubmissions,
        IComputeOwnedSlot[] slots)
    {
        default(ArgumentNullException).ThrowIfNull(slots);
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(maximumPendingSubmissions);

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");
        }

        PipelineDescriptorSet descriptorSet = PipelineDescriptorReader.Read(descriptor);

        default(ArgumentException).ThrowIf(descriptorSet.Kind is not DescriptorKind.PipelineHost, nameof(descriptor));

        PipelineHostDescriptor host = descriptorSet.Host;

        default(ArgumentException).ThrowIf(host.Structural.OwnedSlotCount != slots.Length, nameof(slots));
        default(ArgumentOutOfRangeException).ThrowIfLessThan(maximumPendingSubmissions, host.MaximumConcurrentInvocations);

        SlotResourcePlanStateRecord[] planStates = new SlotResourcePlanStateRecord[host.Structural.OwnedSlotCount];
        int planScalarCount = SlotResourcePlanStorage.CreateHostPlanStates(host, planStates);

        HostStructuralReservation reservation;
        PipelineCommandListPartition? commandLists = null;
        ResourceUsageSetPartition? usageSets = null;

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");
            default(InvalidOperationException).ThrowIf(
                !this.aggregate.TryReserveHost(host, maximumPendingSubmissions, planScalarCount, out reservation),
                "The device structural capacity is exhausted.");
        }

        try
        {
            commandLists = this.commandListPool.CreatePartition(reservation.CommandListEntries);
            usageSets = this.usageSetPool.ReservePartition(maximumPendingSubmissions, host.Structural.MaximumTrackedResourceCount);

            PendingSubmissionRecordPartition pendingRecords = new(maximumPendingSubmissions);
            int[] planStorage = new int[planScalarCount];

            HostRegistrationId id;

            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

                id = new HostRegistrationId(checked(this.nextHostRegistrationId + 1));
            }

            HostRegistrationRecord registration = new(
                id,
                host.MaximumConcurrentInvocations,
                maximumPendingSubmissions,
                host.Structural.MaximumTrackedResourceCount,
                host.Structural.MaximumCommandListSegments,
                host.Structural.OwnedSlotCount);

            PipelineHostRuntime runtime = new(
                host,
                registration,
                reservation,
                commandLists,
                usageSets,
                pendingRecords,
                planStorage,
                planStates,
                slots);

            BindSlots(slots, planStorage, planStates);

            default(InvalidOperationException).ThrowIf(!runtime.TryCommitActive(), "The host registration could not be committed.");

            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

                this.nextHostRegistrationId = id.Value;

                this.hosts.Add(runtime);
            }

            return runtime;
        }
        catch
        {
            UnbindSlots(slots);

            if (usageSets is not null)
            {
                this.usageSetPool.ReleasePartition(usageSets);
            }

            if (commandLists is not null)
            {
                this.commandListPool.DestroyPartition(commandLists);
            }

            lock (this.registrationGate)
            {
                this.aggregate.ReleaseHost(reservation);
            }

            throw;
        }
    }

    public bool TryUnregisterHost(PipelineHostRuntime runtime)
    {
        default(ArgumentNullException).ThrowIfNull(runtime);

        if (!runtime.TryBeginRelease())
        {
            return false;
        }

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(!this.hosts.Remove(runtime), "The host runtime is not registered on the device.");

            this.aggregate.ReleaseHost(runtime.Reservation);
        }

        this.usageSetPool.ReleasePartition(runtime.UsageSets);
        this.commandListPool.DestroyPartition(runtime.CommandLists);

        return runtime.TryCompleteRelease();
    }

    public void Dispose()
    {
        PipelineHostRuntime[] pendingHosts;

        lock (this.registrationGate)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            pendingHosts = [.. this.hosts];

            this.hosts.Clear();
        }

        foreach (PipelineHostRuntime runtime in pendingHosts)
        {
            runtime.RequestDispose();
        }

        this.commandListPool.Dispose();
    }

    private static void BindSlots(IComputeOwnedSlot[] slots, int[] planStorage, SlotResourcePlanStateRecord[] planStates)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            default(ArgumentException).ThrowIf(slots[i] is null, nameof(slots));

            if (!slots[i].TryBind(planStorage, in planStates[i]))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    slots[j].RequestDispose();
                }

                default(InvalidOperationException).ThrowIf(true, "The owned slot is already bound to a pipeline host.");
            }
        }
    }

    private static void UnbindSlots(IComputeOwnedSlot[] slots)
    {
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            slots[i]?.RequestDispose();
        }
    }
}
