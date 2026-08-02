using System;
using System.Runtime.CompilerServices;
using ComputeWeave.Graphics.Extensions;
using ComputeWeave.Graphics.Helpers;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;
using ComputeWeave.Interop.Allocation;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Resources.Plans;
using ComputeWeave.Win32;
using static ComputeWeave.Win32.D3D12_FORMAT_SUPPORT1;
using ResourceType = ComputeWeave.Graphics.Resources.Enums.ResourceType;

#pragma warning disable CA1063

namespace ComputeWeave.Resources;

/// <summary>
/// A <see langword="class"/> representing a typed 2D texture stored on on CPU memory, that can be used to transfer data to/from the GPU.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
public abstract unsafe partial class TransferTexture2D<T> : IReferenceTrackedObject, IGraphicsResource, IResourceGenerationOwner, IGenerationBoundResource
    where T : unmanaged
{
    /// <summary>
    /// The <see cref="ReferenceTracker"/> value for the current instance.
    /// </summary>
    private ReferenceTracker referenceTracker;

    /// <summary>
    /// The generation identity of the current texture.
    /// </summary>
    /// <remarks>
    /// A transfer texture never exchanges generations and never produces usage entries, so the record only
    /// carries its identity and the native references taken on it.
    /// </remarks>
    private ResourceGenerationBinding generationBinding;

    /// <summary>
    /// The resident state of the heap the current texture lives in.
    /// </summary>
    private readonly TrackedResourceState residentState;

    /// <summary>
    /// The memory accounting of <see cref="d3D12Resource"/>, if it is tracked by the memory coordinator.
    /// </summary>
    private GraphicsMemoryAllocation memoryAllocation;

    /// <summary>
    /// The <see cref="ID3D12Allocation"/> instance used to retrieve <see cref="d3D12Resource"/>, if available.
    /// </summary>
    private ComPtr<ID3D12Allocation> allocation;

    /// <summary>
    /// The <see cref="ID3D12Resource"/> instance currently mapped.
    /// </summary>
    private ComPtr<ID3D12Resource> d3D12Resource;

    /// <summary>
    /// The pointer to the start of the mapped buffer data.
    /// </summary>
    private readonly T* mappedData;

    /// <summary>
    /// The <see cref="D3D12_PLACED_SUBRESOURCE_FOOTPRINT"/> description for the current resource.
    /// </summary>
    private readonly D3D12_PLACED_SUBRESOURCE_FOOTPRINT d3D12PlacedSubresourceFootprint;

    /// <summary>
    /// Creates a new <see cref="TransferTexture2D{T}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="ComputeWeave.GraphicsDevice"/> associated with the current instance.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <param name="resourceType">The resource type for the current texture.</param>
    /// <param name="allocationMode">The allocation mode to use for the new resource.</param>
    private protected TransferTexture2D(GraphicsDevice device, int width, int height, ResourceType resourceType, AllocationMode allocationMode)
    {
        using ReferenceTracker.Lease _0 = ReferenceTracker.Create(this, out this.referenceTracker);

        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(width, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(height, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);

        using ReferenceTracker.Lease _1 = device.GetReferenceTracker().GetLease();

        device.ThrowIfDeviceLost();

        if (!device.D3D12Device->IsDxgiFormatSupported(DXGIFormatHelper.GetForType<T>(), D3D12_FORMAT_SUPPORT1_TEXTURE2D))
        {
            UnsupportedTextureTypeException.ThrowForTexture2D<T>();
        }

        GraphicsDevice = device;

        device.D3D12Device->GetCopyableFootprint(
            DXGIFormatHelper.GetForType<T>(),
            (uint)width,
            (uint)height,
            out this.d3D12PlacedSubresourceFootprint,
            out _,
            out ulong totalSizeInBytes);

        this.memoryAllocation = device.CreateOrAllocateResource(
            resourceType,
            allocationMode,
            totalSizeInBytes,
            out this.allocation,
            out this.d3D12Resource);

        this.mappedData = (T*)this.d3D12Resource.Get()->Map().Pointer;

        this.residentState = ComputeGenerationDescriber.GetTransferResidentState(resourceType);

        this.generationBinding.InitializeObservedAccess(ComputeGenerationDescriber.GetTransferObservedAccess(resourceType));

        this.generationBinding.InitializeSelfOwned(
            this,
            device.ResourceIdentities,
            this.residentState,
            this.memoryAllocation.Placement,
            this.memoryAllocation.Bytes);

        this.d3D12Resource.Get()->SetName(this);
    }

    /// <inheritdoc/>
    public GraphicsDevice GraphicsDevice { get; }

    /// <inheritdoc/>
    ResourceGenerationSetId IResourceGenerationOwner.SetId => this.generationBinding.SetId;

    /// <inheritdoc/>
    int IResourceGenerationOwner.ResourceCount => 1;

    /// <inheritdoc/>
    ref ResourceGenerationRecord IResourceGenerationOwner.GetResourceRecord(int resourceOrdinal)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotEqual(resourceOrdinal, 0);

        return ref this.generationBinding.Record;
    }

    /// <inheritdoc/>
    ID3D12Resource* IResourceGenerationOwner.GetResourceNativePointer(int resourceOrdinal)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotEqual(resourceOrdinal, 0);

        return D3D12Resource;
    }

    /// <inheritdoc/>
    void IGenerationBoundResource.BindGeneration(IResourceGenerationOwner owner, int resourceIndex)
    {
        this.generationBinding.BindToOwner(owner, resourceIndex);
    }

    /// <inheritdoc/>
    bool IGenerationBoundResource.TryGetGenerationBinding(out ResourceUsageBinding binding)
    {
        return this.generationBinding.TryGetBinding(this.residentState, out binding);
    }

    /// <summary>
    /// Gets the width of the current texture.
    /// </summary>
    public int Width => (int)this.d3D12PlacedSubresourceFootprint.Footprint.Width;

    /// <summary>
    /// Gets the height of the current texture.
    /// </summary>
    public int Height => (int)this.d3D12PlacedSubresourceFootprint.Footprint.Height;

    /// <summary>
    /// Gets the <see cref="ID3D12Resource"/> instance currently mapped.
    /// </summary>
    internal ID3D12Resource* D3D12Resource => this.d3D12Resource.Get();

    /// <summary>
    /// Gets the <see cref="D3D12_PLACED_SUBRESOURCE_FOOTPRINT"/> value for the current resource.
    /// </summary>
    internal ref readonly D3D12_PLACED_SUBRESOURCE_FOOTPRINT D3D12PlacedSubresourceFootprint => ref this.d3D12PlacedSubresourceFootprint;

    /// <summary>
    /// Gets a <see cref="TextureView2D{T}"/> representing a view over the mapped contents of the current <see cref="TransferTexture2D{T}"/> instance.
    /// </summary>
    /// <remarks>The returned view is only valid while the current <see cref="TransferTexture2D{T}"/> instance is not disposed.</remarks>
    public TextureView2D<T> View
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

            return new(this.mappedData, Width, Height, (int)this.d3D12PlacedSubresourceFootprint.Footprint.RowPitch);
        }
    }

    /// <inheritdoc/>
    void IReferenceTrackedObject.DangerousOnDispose()
    {
        this.d3D12Resource.Dispose();
        this.allocation.Dispose();
        this.memoryAllocation.Dispose();
    }

    /// <summary>
    /// Throws a <see cref="GraphicsDeviceMismatchException"/> if the target device doesn't match the current one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfDeviceMismatch(GraphicsDevice device)
    {
        if (GraphicsDevice != device)
        {
            GraphicsDeviceMismatchException.Throw(this, device);
        }
    }
}