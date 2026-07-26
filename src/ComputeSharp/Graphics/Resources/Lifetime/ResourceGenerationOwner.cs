using System;
using System.Threading;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Win32;

namespace ComputeSharp.Resources.Lifetime;

internal interface IResourceGenerationOwner
{
    ResourceGenerationSetId SetId { get; }

    int ResourceCount { get; }

    ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal);
}

internal sealed unsafe class ResourceGenerationOwner : IResourceGenerationOwner
{
    private readonly GraphicsDevice device;

    private readonly ResourceGenerationRecord[] records;

    private readonly IReferenceTrackedObject?[] resources;

    private readonly ComPtr<ID3D12Resource>[] nativeResources;

    private readonly Lock accountingGate = new();

    private readonly MemoryReservationToken reservation;

    private bool isAccountingCommitted;

    private bool isAccountingSettled;

    private int attachedCount;

    private int releasedCount;

    public ResourceGenerationOwner(
        GraphicsDevice device,
        ResourceIdentityAllocator identities,
        ComputeResourceRecovery recovery,
        in MemoryReservationToken reservation,
        int resourceCount)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(identities);
        default(ArgumentException).ThrowIf(reservation.IsNone, nameof(reservation));
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(resourceCount);

        this.device = device;
        this.reservation = reservation;
        this.records = new ResourceGenerationRecord[resourceCount];
        this.resources = new IReferenceTrackedObject?[resourceCount];
        this.nativeResources = new ComPtr<ID3D12Resource>[resourceCount];

        SetId = identities.CreateGenerationSetId();

        for (int i = 0; i < resourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref this.records[i];

            record.ResourceId = identities.CreateResourceId();
            record.Id = identities.CreateGenerationId();
            record.Placement = reservation.Placement;
            record.Recovery = recovery;
            record.ExternalObjectsReleased = 1;
        }
    }

    public ResourceGenerationSetId SetId { get; }

    public int ResourceCount => this.records.Length;

    public int AttachedCount => this.attachedCount;

    public MemoryPlacement Placement => this.reservation.Placement;

    public ulong AllocationByteLength => this.reservation.Bytes;

    public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
    {
        return ref this.records[resourceOrdinal];
    }

    public TResource? TryGetResource<TResource>(int resourceOrdinal)
        where TResource : class, IGraphicsResource
    {
        return (uint)resourceOrdinal < (uint)this.records.Length ? this.resources[resourceOrdinal] as TResource : null;
    }

    public void AttachResource(
        IReferenceTrackedObject resource,
        ID3D12Resource* d3D12Resource,
        TrackedResourceState d3D12State,
        ulong allocationByteLength)
    {
        default(ArgumentNullException).ThrowIfNull(resource);
        default(ArgumentNullException).ThrowIf(d3D12Resource is null, nameof(d3D12Resource));

        int resourceOrdinal = this.attachedCount;

        default(InvalidOperationException).ThrowIf(
            resourceOrdinal >= this.records.Length,
            "The resource generation owner has no member left to attach.");

        this.resources[resourceOrdinal] = resource;
        this.nativeResources[resourceOrdinal] = new ComPtr<ID3D12Resource>(d3D12Resource);
        this.records[resourceOrdinal].D3D12State = d3D12State;
        this.records[resourceOrdinal].ReclaimableBytes = allocationByteLength;
        this.attachedCount = resourceOrdinal + 1;
    }

    public void CompleteConstruction()
    {
        default(InvalidOperationException).ThrowIf(
            this.attachedCount != this.records.Length,
            "The resource generation owner has members left to attach.");

        for (int i = 0; i < this.records.Length; i++)
        {
            default(InvalidOperationException).ThrowIf(
                !this.records[i].TryCompleteConstruction(),
                "The resource generation is no longer being constructed.");
        }
    }

    public void ReleaseUnpublished()
    {
        for (int i = this.records.Length - 1; i >= 0; i--)
        {
            ref ResourceGenerationRecord record = ref this.records[i];

            if (record.TryFailConstruction())
            {
                ReleaseResource(i);

                continue;
            }

            if (record.TryRequestRetire())
            {
                record.ReleaseOwnerReference();
            }

            _ = record.TryPromoteRetiredReady(this.device.IsFenceCompleted(in record.RetirementFence));

            if (record.TryBeginRelease(ResourceReleaseAuthority.NormalCompletion))
            {
                ReleaseResource(i);

                _ = record.TryCompleteRelease(ResourceReleaseAuthority.NormalCompletion);
            }
        }
    }

    public bool TryReleaseRetired(ResourceReleaseAuthority authority)
    {
        bool isFullyReleased = true;

        for (int i = 0; i < this.records.Length; i++)
        {
            ref ResourceGenerationRecord record = ref this.records[i];

            if (record.ReadLifecycle() is ResourceGenerationState.Released)
            {
                continue;
            }

            _ = record.TryPromoteRetiredReady(this.device.IsFenceCompleted(in record.RetirementFence));

            if (record.TryBeginRelease(authority))
            {
                ReleaseResource(i);

                _ = record.TryCompleteRelease(authority);
            }

            if (record.ReadLifecycle() is not ResourceGenerationState.Released)
            {
                isFullyReleased = false;
            }
        }

        return isFullyReleased;
    }

    public void CommitAccounting()
    {
        lock (this.accountingGate)
        {
            if (this.isAccountingSettled || this.isAccountingCommitted)
            {
                return;
            }

            this.device.CommitMemoryReservation(in this.reservation);

            this.isAccountingCommitted = true;
        }
    }

    private void ReleaseResource(int resourceOrdinal)
    {
        IReferenceTrackedObject? resource = this.resources[resourceOrdinal];

        this.resources[resourceOrdinal] = null;

        resource?.Dispose();

        this.nativeResources[resourceOrdinal].Dispose();

        if (Interlocked.Increment(ref this.releasedCount) == this.records.Length)
        {
            SettleAccounting();
        }
    }

    private void SettleAccounting()
    {
        lock (this.accountingGate)
        {
            if (this.isAccountingSettled)
            {
                return;
            }

            this.isAccountingSettled = true;

            if (this.isAccountingCommitted)
            {
                this.device.ReleaseOwnedMemory(this.reservation.Placement, this.reservation.Bytes);
            }
            else
            {
                this.device.AbortMemoryReservation(in this.reservation);
            }
        }
    }
}

internal readonly struct ResourceGenerationSetHandle
{
    public ResourceGenerationSetHandle(IResourceGenerationOwner owner)
    {
        default(ArgumentNullException).ThrowIfNull(owner);

        if (owner.SetId.Value == 0 || owner.ResourceCount <= 0)
        {
            default(ArgumentException).Throw(nameof(owner), "The generation owner is invalid.");
        }

        Owner = owner;
        SetId = owner.SetId;
    }

    public ResourceGenerationSetId SetId { get; }

    public IResourceGenerationOwner Owner { get; }

    public bool IsEmpty => Owner is null;
}

internal readonly struct ResourceGenerationPin(ResourceGenerationSetHandle handle, ResourceGenerationId generationId, int resourceIndex)
{
    public ResourceGenerationSetHandle Handle { get; } = handle;

    public ResourceGenerationId GenerationId { get; } = generationId;

    public int ResourceIndex { get; } = resourceIndex;
}

internal readonly struct ResourceBindingRecord(
    SlotOrdinal slot,
    ResourceGenerationSetId setId,
    ResourceGenerationId generationId,
    ulong bindingEpoch,
    int resourceIndex)
{
    public SlotOrdinal Slot { get; } = slot;

    public ResourceGenerationSetId SetId { get; } = setId;

    public ResourceGenerationId GenerationId { get; } = generationId;

    public ulong BindingEpoch { get; } = bindingEpoch;

    public int ResourceIndex { get; } = resourceIndex;
}
