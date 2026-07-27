using System;
using System.Collections.Generic;
using System.Threading;
using ComputeSharp.Graphics.Commands.Interop;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Win32;

namespace ComputeSharp.Graphics.Pipelines;

internal sealed unsafe class DeviceRegistrationRegistry : IDisposable
{
    private readonly GraphicsDevice device;

    private readonly PipelineCommandListPool commandListPool;

    private readonly ResourceUsageSetPool usageSetPool = new();

    private readonly ResourceIdentityAllocator identities;

    private readonly List<PipelineHostRuntime> hosts = [];

    private readonly List<ComputeInteropDomain> domains = [];

    private readonly List<InteropResourceSetRuntime> resourceSets = [];

    private readonly CompletionRegistry completions = new();

    private readonly CompletionCoordinator coordinator;

    private readonly Lock registrationGate = new();

    private DeviceStructuralAggregate aggregate;

    private ulong nextHostRegistrationId;

    private ulong nextDomainRegistrationId;

    private ulong nextResourceSetRegistrationId;

    private bool isDisposed;

    public DeviceRegistrationRegistry(GraphicsDevice device, D3D12_COMMAND_LIST_TYPE d3D12CommandListType)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(NotSupportedException).ThrowIf(device.HasOpaqueMemoryAllocator);

        this.device = device;
        this.identities = device.ResourceIdentities;
        this.commandListPool = new PipelineCommandListPool(device.D3D12Device, d3D12CommandListType);
        this.coordinator = new CompletionCoordinator(device, this.completions);

        this.completions.AttachCoordinator(this.coordinator);
    }

    public GraphicsDevice Device => this.device;

    public CompletionRegistry Completions => this.completions;

    public CompletionCoordinator Coordinator => this.coordinator;

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

        OwnedSlotResourceLayout[] slotLayouts = new OwnedSlotResourceLayout[host.Structural.OwnedSlotCount];
        int slotResourceCount = SlotGenerationStorage.CreateHostSlotLayouts(host, slotLayouts);
        ComputeResourceAccess[] slotResourceAccesses = new ComputeResourceAccess[slotResourceCount];

        SlotGenerationStorage.ResolveResourceAccesses(host, slotLayouts, slotResourceAccesses);

        HostStructuralReservation reservation;
        PipelineCommandListPartition? commandLists = null;
        ResourceUsageSetPartition? usageSets = null;
        int boundSlotCount = 0;

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
            RecordingBundlePartition recordingBundles = new(reservation.RecordingBundles, host.Structural.MaximumTrackedResourceCount);
            int[] planStorage = new int[planScalarCount];

            ulong idValue = 0;
            bool isSequenceExhausted;

            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

                isSequenceExhausted = this.nextHostRegistrationId == ulong.MaxValue;

                if (!isSequenceExhausted)
                {
                    idValue = ++this.nextHostRegistrationId;
                }
            }

            if (isSequenceExhausted)
            {
                this.device.ThrowTerminalSequenceExhaustion("host registration identity");
            }

            HostRegistrationId id = new(idValue);

            HostRegistrationRecord registration = new(
                id,
                host.MaximumConcurrentInvocations,
                maximumPendingSubmissions,
                host.Structural.MaximumTrackedResourceCount,
                host.Structural.MaximumCommandListSegments,
                host.Structural.OwnedSlotCount);

            PipelineHostRuntime runtime = new(
                this,
                this.device,
                host,
                registration,
                reservation,
                commandLists,
                usageSets,
                pendingRecords,
                recordingBundles,
                planStorage,
                planStates,
                slotLayouts,
                slotResourceAccesses,
                new ComputeGenerationDeclaration[slotResourceCount],
                this.identities,
                slots);

            BindSlots(this, slots, planStorage, planStates);

            boundSlotCount = slots.Length;

            default(InvalidOperationException).ThrowIf(!runtime.TryCommitActive(), "The host registration could not be committed.");

            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

                this.hosts.Add(runtime);
            }

            return runtime;
        }
        catch
        {
            UnbindSlots(slots, boundSlotCount);

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

    public InteropResourceSetRuntime RegisterResourceSet(
        ComputeInteropDomain domain,
        ReadOnlySpan<byte> descriptor,
        IComputeSharedSlot[] slots)
    {
        default(ArgumentNullException).ThrowIfNull(domain);
        default(ArgumentNullException).ThrowIfNull(slots);
        default(ArgumentException).ThrowIf(!ReferenceEquals(domain.Device, this.device), nameof(domain));

        PipelineDescriptorSet descriptorSet = PipelineDescriptorReader.Read(descriptor);

        default(ArgumentException).ThrowIf(descriptorSet.Kind is not DescriptorKind.InteropResourceSet, nameof(descriptor));

        InteropResourceSetDescriptor resourceSet = descriptorSet.ResourceSet;

        default(ArgumentException).ThrowIf(resourceSet.Structural.SharedTextureSlotCount != slots.Length, nameof(slots));

        SlotResourcePlanStateRecord[] planStates = new SlotResourcePlanStateRecord[slots.Length];
        int planScalarCount = SlotResourcePlanStorage.CreateResourceSetPlanStates(resourceSet, planStates);

        default(InvalidOperationException).ThrowIf(
            !domain.TryAcquireReference(ExternalDomainReference.ResourceSet),
            "The interop domain no longer accepts resource set registrations.");

        ResourceSetStructuralReservation reservation = default;
        bool isBaselineReserved = false;
        int boundSlotCount = 0;

        try
        {
            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");
                default(InvalidOperationException).ThrowIf(
                    !this.aggregate.TryReserveResourceSet(resourceSet, planScalarCount, out reservation),
                    "The device structural capacity is exhausted.");
            }

            isBaselineReserved = true;

            int[] planStorage = new int[planScalarCount];

            ResourceSetRegistrationRecord registration = new(AllocateResourceSetId(), slots.Length);

            InteropResourceSetRuntime runtime = new(
                this,
                this.device,
                domain,
                in resourceSet,
                in registration,
                in reservation,
                planStorage,
                planStates,
                slots);

            BindSharedSlots(runtime, slots, planStorage, planStates);

            boundSlotCount = slots.Length;

            default(InvalidOperationException).ThrowIf(!runtime.TryCommitActive(), "The resource set registration could not be committed.");

            lock (this.registrationGate)
            {
                default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

                this.resourceSets.Add(runtime);
            }

            return runtime;
        }
        catch
        {
            UnbindSharedSlots(slots, boundSlotCount);

            if (isBaselineReserved)
            {
                lock (this.registrationGate)
                {
                    this.aggregate.ReleaseResourceSet(in reservation);
                }
            }

            domain.ReleaseReference(ExternalDomainReference.ResourceSet);

            throw;
        }
    }

    public bool TryUnregisterResourceSet(InteropResourceSetRuntime runtime)
    {
        default(ArgumentNullException).ThrowIfNull(runtime);

        if (!runtime.TryBeginRelease())
        {
            return false;
        }

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(
                !this.resourceSets.Remove(runtime) && !this.isDisposed,
                "The interop resource set is not registered on the device.");

            this.aggregate.ReleaseResourceSet(runtime.Reservation);
        }

        if (!runtime.TryCompleteRelease())
        {
            return false;
        }

        runtime.Domain.ReleaseReference(ExternalDomainReference.ResourceSet);

        this.coordinator.Wake();

        return true;
    }

    public ExternalDomainId AllocateDomainId()
    {
        ulong idValue = 0;
        bool isSequenceExhausted;

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

            isSequenceExhausted = this.nextDomainRegistrationId == ulong.MaxValue;

            if (!isSequenceExhausted)
            {
                idValue = ++this.nextDomainRegistrationId;
            }
        }

        if (isSequenceExhausted)
        {
            this.device.ThrowTerminalSequenceExhaustion("interop domain identity");
        }

        return new ExternalDomainId(idValue);
    }

    public void PublishDomain(ComputeInteropDomain domain)
    {
        default(ArgumentNullException).ThrowIfNull(domain);

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

            this.domains.Add(domain);
        }
    }

    public void UnregisterDomain(ComputeInteropDomain domain)
    {
        default(ArgumentNullException).ThrowIfNull(domain);

        lock (this.registrationGate)
        {
            _ = this.domains.Remove(domain);
        }

        this.coordinator.Wake();
    }

    public void MarkDomainsDeviceTerminal(Exception reason)
    {
        ComputeInteropDomain[] registeredDomains;

        lock (this.registrationGate)
        {
            registeredDomains = [.. this.domains];
        }

        foreach (ComputeInteropDomain domain in registeredDomains)
        {
            domain.MarkDeviceTerminal(reason);
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
            default(InvalidOperationException).ThrowIf(
                !this.hosts.Remove(runtime) && !this.isDisposed,
                "The host runtime is not registered on the device.");

            this.aggregate.ReleaseHost(runtime.Reservation);
        }

        this.usageSetPool.ReleasePartition(runtime.UsageSets);
        this.commandListPool.DestroyPartition(runtime.CommandLists);

        if (!runtime.TryCompleteRelease())
        {
            return false;
        }

        this.coordinator.Wake();

        return true;
    }

    public void MarkGenerationsTerminalRetained()
    {
        PipelineHostRuntime[] registeredHosts;
        InteropResourceSetRuntime[] registeredResourceSets;

        lock (this.registrationGate)
        {
            registeredHosts = [.. this.hosts];
            registeredResourceSets = [.. this.resourceSets];
        }

        foreach (PipelineHostRuntime runtime in registeredHosts)
        {
            runtime.MarkOwnedSlotsTerminalRetained();
        }

        foreach (InteropResourceSetRuntime runtime in registeredResourceSets)
        {
            runtime.MarkSharedSlotsTerminalRetained();
        }
    }

    public void Dispose()
    {
        PipelineHostRuntime[] pendingHosts;
        InteropResourceSetRuntime[] pendingResourceSets;
        ComputeInteropDomain[] pendingDomains;

        lock (this.registrationGate)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            pendingHosts = [.. this.hosts];
            pendingResourceSets = [.. this.resourceSets];
            pendingDomains = [.. this.domains];
        }

        this.coordinator.Dispose();

        try
        {
            ReleaseRegisteredHosts(pendingHosts);
        }
        finally
        {
            try
            {
                ReleaseRegisteredResourceSets(pendingResourceSets);
            }
            finally
            {
                try
                {
                    ReleaseRegisteredDomains(pendingDomains);
                }
                finally
                {
                    this.commandListPool.Dispose();
                }
            }
        }
    }

    private void ReleaseRegisteredResourceSets(InteropResourceSetRuntime[] pendingResourceSets)
    {
        foreach (InteropResourceSetRuntime runtime in pendingResourceSets)
        {
            runtime.RequestDispose();

            if (this.device.IsDeviceTerminal)
            {
                runtime.ReleaseSharedSlotTerminalGenerations();
            }
            else
            {
                runtime.RunSharedSlotMaintenance();
            }

            _ = TryUnregisterResourceSet(runtime);
        }

        lock (this.registrationGate)
        {
            this.resourceSets.Clear();
        }
    }

    private void ReleaseRegisteredDomains(ComputeInteropDomain[] pendingDomains)
    {
        foreach (ComputeInteropDomain domain in pendingDomains)
        {
            domain.ReleaseForDeviceTeardown();
        }

        lock (this.registrationGate)
        {
            this.domains.Clear();
        }
    }

    private void ReleaseRegisteredHosts(PipelineHostRuntime[] pendingHosts)
    {
        foreach (PipelineHostRuntime runtime in pendingHosts)
        {
            runtime.RequestDispose();
        }

        if (this.device.IsDeviceTerminal)
        {
            foreach (PipelineHostRuntime runtime in pendingHosts)
            {
                runtime.ReleaseOwnedSlotTerminalGenerations();
            }
        }
        else
        {
            while (this.completions.TryGetMaximumCommittedFence(out ulong fenceValue))
            {
                this.device.WaitForComputeFenceValue(fenceValue);

                while (ComputeSubmissionExecutor.TryReleaseCompleted(this.device, this.completions))
                {
                }
            }

            foreach (PipelineHostRuntime runtime in pendingHosts)
            {
                runtime.RunOwnedSlotMaintenance();

                _ = TryUnregisterHost(runtime);
            }
        }

        lock (this.registrationGate)
        {
            this.hosts.Clear();
        }
    }

    private ResourceSetRegistrationId AllocateResourceSetId()
    {
        ulong idValue = 0;
        bool isSequenceExhausted;

        lock (this.registrationGate)
        {
            default(InvalidOperationException).ThrowIf(this.isDisposed, "The device no longer accepts registrations.");

            isSequenceExhausted = this.nextResourceSetRegistrationId == ulong.MaxValue;

            if (!isSequenceExhausted)
            {
                idValue = ++this.nextResourceSetRegistrationId;
            }
        }

        if (isSequenceExhausted)
        {
            this.device.ThrowTerminalSequenceExhaustion("resource set registration identity");
        }

        return new ResourceSetRegistrationId(idValue);
    }

    private static void BindSharedSlots(
        InteropResourceSetRuntime runtime,
        IComputeSharedSlot[] slots,
        int[] planStorage,
        SlotResourcePlanStateRecord[] planStates)
    {
        int boundSlotCount = 0;

        try
        {
            for (int i = 0; i < slots.Length; i++)
            {
                default(ArgumentException).ThrowIf(slots[i] is null, nameof(slots));
                default(InvalidOperationException).ThrowIf(
                    !slots[i].TryBind(runtime, planStorage, in planStates[i]),
                    "The shared texture slot is already bound to a resource set.");

                boundSlotCount = i + 1;
            }
        }
        catch
        {
            UnbindSharedSlots(slots, boundSlotCount);

            throw;
        }
    }

    private static void UnbindSharedSlots(IComputeSharedSlot[] slots, int boundSlotCount)
    {
        for (int i = boundSlotCount - 1; i >= 0; i--)
        {
            slots[i]?.RequestDispose();
        }
    }

    private static void BindSlots(
        DeviceRegistrationRegistry registry,
        IComputeOwnedSlot[] slots,
        int[] planStorage,
        SlotResourcePlanStateRecord[] planStates)
    {
        int boundSlotCount = 0;

        try
        {
            for (int i = 0; i < slots.Length; i++)
            {
                default(ArgumentException).ThrowIf(slots[i] is null, nameof(slots));
                default(InvalidOperationException).ThrowIf(
                    !slots[i].TryBind(registry, planStorage, in planStates[i]),
                    "The owned slot is already bound to a pipeline host.");

                boundSlotCount = i + 1;
            }
        }
        catch
        {
            UnbindSlots(slots, boundSlotCount);

            throw;
        }
    }

    private static void UnbindSlots(IComputeOwnedSlot[] slots, int boundSlotCount)
    {
        for (int i = boundSlotCount - 1; i >= 0; i--)
        {
            slots[i]?.RequestDispose();
        }
    }
}
