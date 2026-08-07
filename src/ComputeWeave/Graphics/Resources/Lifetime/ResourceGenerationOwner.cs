using System;
using System.Threading;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;
using ComputeWeave.Memory;
using ComputeWeave.Win32;

namespace ComputeWeave.Resources.Lifetime;

internal unsafe interface IResourceGenerationOwner
{
    ResourceGenerationSetId SetId { get; }

    int ResourceCount { get; }

    ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal);

    ID3D12Resource* GetResourceNativePointer(int resourceOrdinal);
}

internal sealed unsafe class ResourceGenerationOwner : IResourceGenerationOwner
{
    private readonly GraphicsDevice device;

    private ResourceGenerationRecord record0;
    private readonly ResourceGenerationRecord[]? records;

    private IReferenceTrackedObject? resource0;
    private readonly IReferenceTrackedObject?[]? resources;

    private ComPtr<ID3D12Resource> nativeResource0;
    private readonly ComPtr<ID3D12Resource>[]? nativeResources;

    private IDisposable? externalObject0;
    private readonly IDisposable?[]? externalObjects;

    private readonly Lock accountingGate = new();

    private readonly MemoryReservationToken reservation;

    private bool isAccountingCommitted;

    private bool isAccountingSettled;

    private int attachedCount;

    private int releasedCount;

    private int isExternalObjectsReleased;

    public ResourceGenerationOwner(
        GraphicsDevice device,
        ResourceIdentityAllocator identities,
        ComputeResourceRecovery recovery,
        in MemoryReservationToken reservation,
        int resourceCount,
        ComputeInteropDomain? domain)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(identities);
        default(ArgumentException).ThrowIf(reservation.IsNone, nameof(reservation));
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(resourceCount);

        this.device = device;
        this.reservation = reservation;
        Domain = domain;
        this.ResourceCount = resourceCount;

        if (resourceCount > 1)
        {
            this.records = new ResourceGenerationRecord[resourceCount];
            this.resources = new IReferenceTrackedObject?[resourceCount];
            this.nativeResources = new ComPtr<ID3D12Resource>[resourceCount];
            this.externalObjects = new IDisposable?[resourceCount];
        }

        SetId = identities.CreateGenerationSetId();

        for (int i = 0; i < resourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref GetResourceRecord(i);

            record.ResourceId = identities.CreateResourceId();
            record.Id = identities.CreateGenerationId();
            record.StateFlags = domain is null ? ResourceGenerationRecord.ExternalObjectsReleasedBit : 0;
            record.Placement = reservation.Placement;
            record.Recovery = recovery;
        }
    }

    public ResourceGenerationSetId SetId { get; }

    public ComputeInteropDomain? Domain { get; }

    public int ResourceCount { get; }

    public int AttachedCount => this.attachedCount;

    public MemoryPlacement Placement => this.reservation.Placement;

    public ulong AllocationByteLength => this.reservation.Bytes;

    public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
    {
        if (this.records is null)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotEqual(resourceOrdinal, 0);

            return ref this.record0;
        }

        return ref this.records[resourceOrdinal];
    }

    public ID3D12Resource* GetResourceNativePointer(int resourceOrdinal)
    {
        if (this.nativeResources is null)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotEqual(resourceOrdinal, 0);

            return this.nativeResource0.Get();
        }

        return this.nativeResources[resourceOrdinal].Get();
    }

    public TResource? TryGetResource<TResource>(int resourceOrdinal)
        where TResource : class, IGraphicsResource
    {
        if ((uint)resourceOrdinal >= (uint)ResourceCount)
        {
            return null;
        }

        if (this.resources is null)
        {
            return this.resource0 as TResource;
        }

        return this.resources[resourceOrdinal] as TResource;
    }

    public TView? TryGetExternalObject<TView>(int resourceOrdinal)
        where TView : class
    {
        if ((uint)resourceOrdinal >= (uint)ResourceCount)
        {
            return null;
        }

        if (this.externalObjects is null)
        {
            return Volatile.Read(ref this.externalObject0) as TView;
        }

        return Volatile.Read(ref this.externalObjects[resourceOrdinal]) as TView;
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
            resourceOrdinal >= ResourceCount,
            "The resource generation owner has no member left to attach.");

        default(ArgumentException).ThrowIf(resource is not IGenerationBoundResource, nameof(resource));

        ((IGenerationBoundResource)resource).BindGeneration(this, resourceOrdinal);

        if (this.records is null)
        {
            this.resource0 = resource;
            this.nativeResource0 = new ComPtr<ID3D12Resource>(d3D12Resource);
            this.record0.D3D12State = d3D12State;
            this.record0.ReclaimableBytes = allocationByteLength;
        }
        else
        {
            this.resources![resourceOrdinal] = resource;
            this.nativeResources![resourceOrdinal] = new ComPtr<ID3D12Resource>(d3D12Resource);
            this.records[resourceOrdinal].D3D12State = d3D12State;
            this.records[resourceOrdinal].ReclaimableBytes = allocationByteLength;
        }

        this.attachedCount = resourceOrdinal + 1;
    }

    public void AttachExternalObject(int resourceOrdinal, IDisposable externalObject)
    {
        default(ArgumentNullException).ThrowIfNull(externalObject);

        if (this.externalObjects is null)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotInRange(resourceOrdinal, 0, 1);
            default(InvalidOperationException).ThrowIf(
                this.record0.IsExternalObjectsReleased,
                "The resource generation declares no external object to attach.");
            default(InvalidOperationException).ThrowIf(
                this.externalObject0 is not null,
                "The resource generation already owns an external object for that resource.");

            this.externalObject0 = externalObject;
        }
        else
        {
            default(ArgumentOutOfRangeException).ThrowIfNotInRange(resourceOrdinal, 0, this.externalObjects.Length);
            default(InvalidOperationException).ThrowIf(
                this.records![resourceOrdinal].IsExternalObjectsReleased,
                "The resource generation declares no external object to attach.");
            default(InvalidOperationException).ThrowIf(
                this.externalObjects[resourceOrdinal] is not null,
                "The resource generation already owns an external object for that resource.");

            this.externalObjects[resourceOrdinal] = externalObject;
        }
    }

    public bool TryReleaseExternalObjects()
    {
        if (Interlocked.Exchange(ref this.isExternalObjectsReleased, 1) != 0)
        {
            return false;
        }

        for (int i = ResourceCount - 1; i >= 0; i--)
        {
            ReleaseExternalObject(i);
        }

        for (int i = 0; i < ResourceCount; i++)
        {
            GetResourceRecord(i).MarkExternalObjectsReleased();
        }

        return true;
    }

    public void CompleteConstruction()
    {
        default(InvalidOperationException).ThrowIf(
            this.attachedCount != ResourceCount,
            "The resource generation owner has members left to attach.");

        for (int i = 0; i < ResourceCount; i++)
        {
            default(InvalidOperationException).ThrowIf(
                !GetResourceRecord(i).TryCompleteConstruction(),
                "The resource generation is no longer being constructed.");
        }
    }

    public void ReleaseUnpublished()
    {
        for (int i = ResourceCount - 1; i >= 0; i--)
        {
            ref ResourceGenerationRecord record = ref GetResourceRecord(i);

            if (record.TryFailConstruction())
            {
                ReleaseResource(i);

                continue;
            }

            if (record.TryRequestRetire())
            {
                record.ReleaseOwnerReference();
            }

            _ = record.TryPromoteRetiredReady(this.device.IsFenceCompleted(record.RetirementFence));

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

        for (int i = 0; i < ResourceCount; i++)
        {
            ref ResourceGenerationRecord record = ref GetResourceRecord(i);

            if (record.ReadLifecycle() is ResourceGenerationState.Released)
            {
                continue;
            }

            _ = record.TryPromoteRetiredReady(this.device.IsFenceCompleted(record.RetirementFence));

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
        ReleaseExternalObject(resourceOrdinal);

        IReferenceTrackedObject? resource;

        if (this.resources is null)
        {
            resource = this.resource0;
            this.resource0 = null;
            this.nativeResource0.Dispose();
        }
        else
        {
            resource = this.resources[resourceOrdinal];
            this.resources[resourceOrdinal] = null;
            this.nativeResources![resourceOrdinal].Dispose();
        }

        resource?.Dispose();

        if (Interlocked.Increment(ref this.releasedCount) == ResourceCount)
        {
            SettleAccounting();
        }
    }

    private void ReleaseExternalObject(int resourceOrdinal)
    {
        if (this.externalObjects is null)
        {
            Interlocked.Exchange(ref this.externalObject0, null)?.Dispose();
        }
        else
        {
            Interlocked.Exchange(ref this.externalObjects[resourceOrdinal], null)?.Dispose();
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
