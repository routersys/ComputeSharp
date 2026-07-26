using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Win32;

namespace ComputeSharp;

/// <summary>
/// The runtime of a generated compute pipeline host, owning its registration and its owned slot generations.
/// </summary>
public sealed class ComputeHostRuntime : IDisposable
{
    /// <summary>
    /// The registry the current host is registered on.
    /// </summary>
    private readonly DeviceRegistrationRegistry registry;

    /// <summary>
    /// The registration of the current host.
    /// </summary>
    private readonly PipelineHostRuntime runtime;

    /// <summary>
    /// Creates a new <see cref="ComputeHostRuntime"/> instance with the specified parameters.
    /// </summary>
    /// <param name="registry">The registry the current host is registered on.</param>
    /// <param name="runtime">The registration of the current host.</param>
    private ComputeHostRuntime(DeviceRegistrationRegistry registry, PipelineHostRuntime runtime)
    {
        this.registry = registry;
        this.runtime = runtime;
    }

    /// <summary>
    /// Gets the device the current host is registered on.
    /// </summary>
    public GraphicsDevice Device => this.runtime.Device;

    /// <summary>
    /// Gets whether disposal of the current host has been requested.
    /// </summary>
    public bool IsDisposeRequested => this.runtime.State is not (RegistrationState.Constructing or RegistrationState.Active);

    /// <summary>
    /// Registers a generated compute pipeline host on a given device.
    /// </summary>
    /// <param name="device">The device to register the host on.</param>
    /// <param name="canonicalDescriptor">The canonical binary descriptor of the host.</param>
    /// <param name="maximumPendingSubmissions">The maximum number of pending submissions to reserve for the host.</param>
    /// <param name="ownedSlots">The owned slots declared by the host, in slot ordinal order.</param>
    /// <returns>The <see cref="ComputeHostRuntime"/> instance of the registered host.</returns>
    /// <exception cref="ArgumentException">Thrown if the descriptor or the owned slots are not valid.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the device cannot accept the registration.</exception>
    public static ComputeHostRuntime Create(
        GraphicsDevice device,
        ReadOnlySpan<byte> canonicalDescriptor,
        int maximumPendingSubmissions,
        ReadOnlySpan<IComputeOwnedResourceSlot> ownedSlots)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.ThrowIfDeviceLost();

        IComputeOwnedSlot[] slots = new IComputeOwnedSlot[ownedSlots.Length];

        for (int i = 0; i < ownedSlots.Length; i++)
        {
            default(ArgumentException).ThrowIf(ownedSlots[i] is not IComputeOwnedSlot, nameof(ownedSlots));

            slots[i] = (IComputeOwnedSlot)ownedSlots[i];
        }

        DeviceRegistrationRegistry registry = device.GetRegistrationRegistry();

        return new ComputeHostRuntime(registry, registry.RegisterHost(canonicalDescriptor, maximumPendingSubmissions, slots));
    }

    /// <summary>
    /// Ensures the resources of an owned slot match a requested resource plan.
    /// </summary>
    /// <typeparam name="TMaterializer">The type of the materializer declaring the resources of the slot.</typeparam>
    /// <param name="slotOrdinal">The ordinal of the owned slot to ensure.</param>
    /// <param name="requestedPlan">The requested plan vector, in plan field ordinal order.</param>
    /// <param name="materializer">The materializer declaring the resources of the slot.</param>
    /// <param name="changed">Whether a new generation was published.</param>
    /// <returns>Whether the owned slot matches <paramref name="requestedPlan"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if disposal of the current host has been requested.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="requestedPlan"/> does not match the slot contract.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the declared resources do not match the slot contract.</exception>
    /// <exception cref="UnsupportedDoubleOperationException">Thrown if the declared resources need double precision support the device does not have.</exception>
    /// <exception cref="GraphicsMemoryAllocationException">Thrown if a native allocation fails for a reason other than memory pressure.</exception>
    public bool TryEnsureResource<TMaterializer>(
        int slotOrdinal,
        ReadOnlySpan<int> requestedPlan,
        in TMaterializer materializer,
        out bool changed)
        where TMaterializer : struct, IComputeGenerationMaterializer
    {
        changed = false;

        using ReferenceTracker.Lease _0 = Device.GetReferenceTracker().GetLease();

        Device.ThrowIfDeviceLost();

        default(ObjectDisposedException).ThrowIf(IsDisposeRequested, this);

        default(ArgumentOutOfRangeException).ThrowIfNotInRange(slotOrdinal, 0, this.runtime.SlotCount);

        IComputeOwnedSlot slot = this.runtime.GetSlot(slotOrdinal);

        slot.ThrowIfUnbound();

        ref readonly OwnedSlotDescriptor descriptor = ref this.runtime.Descriptor.Slots.Span[slotOrdinal];

        ValidateRequestedPlan(in descriptor, requestedPlan);

        slot.RunMaintenance();

        ResourcePlanDecision decision = slot.Evaluate(in descriptor, requestedPlan);

        if (decision is ResourcePlanDecision.Identical)
        {
            return true;
        }

        if (decision is ResourcePlanDecision.LogicalUpdate)
        {
            slot.GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch);

            return slot.TryApplyLogicalUpdate(activeSetId, bindingEpoch, requestedPlan);
        }

        return TryReplaceGeneration(slot, slotOrdinal, in descriptor, requestedPlan, in materializer, out changed);
    }

    /// <summary>
    /// Gets the binding of a single resource of an owned slot.
    /// </summary>
    /// <typeparam name="TResource">The type of the bound graphics resource.</typeparam>
    /// <param name="slotOrdinal">The ordinal of the owned slot to bind from.</param>
    /// <param name="resourceIndex">The index of the resource within the slot.</param>
    /// <returns>The binding of the requested resource, or an invalid binding if the slot owns no matching generation.</returns>
    public ComputeResourceBinding<TResource> GetBinding<TResource>(int slotOrdinal, int resourceIndex)
        where TResource : class, IGraphicsResource
    {
        default(ArgumentOutOfRangeException).ThrowIfNotInRange(slotOrdinal, 0, this.runtime.SlotCount);

        IComputeOwnedSlot slot = this.runtime.GetSlot(slotOrdinal);

        return slot.TryGetBinding(resourceIndex, out ComputeResourceBinding<TResource> binding) ? binding : default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.runtime.RequestDispose();

        _ = this.registry.TryUnregisterHost(this.runtime);
    }

    /// <summary>
    /// Waits for the disposal of the current host to complete.
    /// </summary>
    public void WaitForDisposal()
    {
        _ = this.registry.TryUnregisterHost(this.runtime);

        default(InvalidOperationException).ThrowIf(
            !this.runtime.IsDisposalComplete,
            "The compute pipeline host is still registered on the device.");
    }

    /// <summary>
    /// Replaces the generation owned by a slot with one materialized for a requested plan.
    /// </summary>
    /// <typeparam name="TMaterializer">The type of the materializer declaring the resources of the slot.</typeparam>
    /// <param name="slot">The owned slot to replace the generation of.</param>
    /// <param name="slotOrdinal">The ordinal of <paramref name="slot"/>.</param>
    /// <param name="descriptor">The descriptor of <paramref name="slot"/>.</param>
    /// <param name="requestedPlan">The requested plan vector, in plan field ordinal order.</param>
    /// <param name="materializer">The materializer declaring the resources of the slot.</param>
    /// <param name="changed">Whether a new generation was published.</param>
    /// <returns>Whether a new generation was published.</returns>
    private bool TryReplaceGeneration<TMaterializer>(
        IComputeOwnedSlot slot,
        int slotOrdinal,
        in OwnedSlotDescriptor descriptor,
        ReadOnlySpan<int> requestedPlan,
        in TMaterializer materializer,
        out bool changed)
        where TMaterializer : struct, IComputeGenerationMaterializer
    {
        changed = false;

        GraphicsDevice device = Device;

        if (TMaterializer.RequiresDoublePrecisionSupport && !device.IsDoublePrecisionSupportAvailable())
        {
            UnsupportedDoubleOperationException.Throw(descriptor.MemberMetadataName);
        }

        OwnedSlotResourceLayout layout = this.runtime.SlotLayouts[slotOrdinal];

        if (!layout.HasAccessContracts)
        {
            throw new InvalidOperationException(
                $"""The owned slot "{descriptor.MemberMetadataName}" has no access contract to materialize its resources with.""");
        }

        Span<ComputeGenerationDeclaration> declarations = this.runtime.SlotResourceDeclarations.AsSpan(layout.StorageOffset, layout.ResourceCount);
        ReadOnlySpan<ComputeResourceAccess> accesses = this.runtime.SlotResourceAccesses.AsSpan(layout.StorageOffset, layout.ResourceCount);

        ComputeGenerationContext describeContext = new(device, declarations, accesses, null);

        materializer.Materialize(ref describeContext);

        ThrowIfDeclarationsAreInvalid(describeContext.Status, describeContext.DeclarationCount, layout.ResourceCount);
        ThrowIfPlanIsInvalid(ComputeGenerationDescriber.ValidateAgainstPlan(in descriptor, requestedPlan, declarations));
        ThrowIfPlanIsInvalid(ComputeGenerationDescriber.ValidatePlacement(declarations, out MemoryPlacement placement, out ulong sizeInBytes));

        if (device.TryReserveMemory(placement, sizeInBytes, out MemoryReservationToken token) is not MemoryAdmissionStatus.Admitted)
        {
            return false;
        }

        ResourceGenerationOwner owner = new(device, this.runtime.Identities, descriptor.Recovery, in token, layout.ResourceCount);
        ulong preparedToken = this.runtime.CreatePreparedToken();

        if (!slot.TryInstallPrepared(new ResourceGenerationSetHandle(owner), preparedToken, requestedPlan))
        {
            owner.ReleaseUnpublished();

            return false;
        }

        slot.GetActiveSnapshot(out ResourceGenerationSetId activeSetId, out ulong bindingEpoch);

        ComputeGenerationContext createContext = new(device, declarations, accesses, owner);

        try
        {
            materializer.Materialize(ref createContext);
        }
        catch
        {
            AbortPreparedGeneration(slot, owner, preparedToken);

            throw;
        }

        if (createContext.Status is not ComputeGenerationDeclarationStatus.Valid ||
            createContext.DeclarationCount != layout.ResourceCount)
        {
            ComputeGenerationDeclarationStatus status = createContext.Status;
            NativeAllocationOutcome outcome = createContext.Outcome;
            HRESULT hresult = createContext.NativeResult;

            AbortPreparedGeneration(slot, owner, preparedToken);

            if (status is not ComputeGenerationDeclarationStatus.NativeCreationFailed)
            {
                ThrowIfPlanIsInvalid(status is ComputeGenerationDeclarationStatus.Valid
                    ? ComputeGenerationDeclarationStatus.CountMismatch
                    : status);
            }

            if (outcome is NativeAllocationOutcome.OutOfMemory)
            {
                return false;
            }

            throw device.CreateNativeAllocationException(outcome, hresult, sizeInBytes);
        }

        owner.CompleteConstruction();

        if (!slot.TryCommitReplacement(activeSetId, bindingEpoch, preparedToken, out _))
        {
            owner.ReleaseUnpublished();

            return false;
        }

        owner.CommitAccounting();

        slot.RunMaintenance();

        changed = true;

        return true;
    }

    /// <summary>
    /// Detaches and releases a prepared generation that was not published.
    /// </summary>
    /// <param name="slot">The owned slot holding the prepared generation.</param>
    /// <param name="owner">The prepared generation to release.</param>
    /// <param name="preparedToken">The token the prepared generation was installed with.</param>
    private static void AbortPreparedGeneration(IComputeOwnedSlot slot, ResourceGenerationOwner owner, ulong preparedToken)
    {
        _ = slot.TryAbortReplacement(preparedToken, out _);

        owner.ReleaseUnpublished();
    }

    /// <summary>
    /// Validates a requested plan vector against the contract of an owned slot.
    /// </summary>
    /// <param name="descriptor">The descriptor of the owned slot.</param>
    /// <param name="requestedPlan">The requested plan vector, in plan field ordinal order.</param>
    private static void ValidateRequestedPlan(in OwnedSlotDescriptor descriptor, ReadOnlySpan<int> requestedPlan)
    {
        default(ArgumentException).ThrowIf(requestedPlan.Length != descriptor.PlanFields.Length, nameof(requestedPlan));

        for (int i = 0; i < requestedPlan.Length; i++)
        {
            default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(requestedPlan[i], nameof(requestedPlan));
        }
    }

    /// <summary>
    /// Throws if the declarations of a materializer do not cover the resources of an owned slot.
    /// </summary>
    /// <param name="status">The status of the completed declarations.</param>
    /// <param name="declarationCount">The number of completed declarations.</param>
    /// <param name="resourceCount">The number of resources of the owned slot.</param>
    private static void ThrowIfDeclarationsAreInvalid(
        ComputeGenerationDeclarationStatus status,
        int declarationCount,
        int resourceCount)
    {
        if (status is not ComputeGenerationDeclarationStatus.Valid)
        {
            throw new InvalidOperationException($"The declarations of the generated resource plan are not valid ({status}).");
        }

        if (declarationCount != resourceCount)
        {
            throw new InvalidOperationException(
                $"The generated resource plan declared {declarationCount} resources instead of {resourceCount}.");
        }
    }

    /// <summary>
    /// Throws if a generated resource plan was refused.
    /// </summary>
    /// <param name="status">The status the plan was refused with.</param>
    private static void ThrowIfPlanIsInvalid(ComputeGenerationDeclarationStatus status)
    {
        if (status is not ComputeGenerationDeclarationStatus.Valid)
        {
            throw new InvalidOperationException($"The generated resource plan is not valid ({status}).");
        }
    }
}
