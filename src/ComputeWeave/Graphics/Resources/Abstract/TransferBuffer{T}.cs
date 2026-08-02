using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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
using static ComputeWeave.Win32.D3D12_FEATURE;
using ResourceType = ComputeWeave.Graphics.Resources.Enums.ResourceType;

#pragma warning disable CA1063, CA2213

namespace ComputeWeave.Resources;

/// <summary>
/// A <see langword="class"/> representing a typed buffer stored on CPU memory, that can be used to transfer data to/from the GPU.
/// </summary>
/// <typeparam name="T">The type of items stored on the buffer.</typeparam>
public abstract unsafe partial class TransferBuffer<T> : IReferenceTrackedObject, IGraphicsResource, IResourceGenerationOwner, IGenerationBoundResource, IMemoryOwner<T>
    where T : unmanaged
{
    /// <summary>
    /// The <see cref="ReferenceTracker"/> value for the current instance.
    /// </summary>
    private ReferenceTracker referenceTracker;

    /// <summary>
    /// The generation identity of the current buffer.
    /// </summary>
    /// <remarks>
    /// A transfer buffer never exchanges generations and never produces usage entries, so the record only
    /// carries its identity and the native references taken on it.
    /// </remarks>
    private ResourceGenerationBinding generationBinding;

    /// <summary>
    /// The resident state of the heap the current buffer lives in.
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
    /// Creates a new <see cref="TransferBuffer{T}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> associated with the current instance.</param>
    /// <param name="length">The number of items to store in the current buffer.</param>
    /// <param name="resourceType">The resource type for the current buffer.</param>
    /// <param name="allocationMode">The allocation mode to use for the new resource.</param>
    [RequiresUnreferencedCode("This method reads type info of all fields of the resource element type (recursively).")]
    private protected TransferBuffer(GraphicsDevice device, int length, ResourceType resourceType, AllocationMode allocationMode)
    {
        using ReferenceTracker.Lease _0 = ReferenceTracker.Create(this, out this.referenceTracker);

        // The maximum length is set such that the aligned buffer size can't exceed uint.MaxValue
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(length, 1, (uint.MaxValue / (uint)sizeof(T)) & ~255);

        using ReferenceTracker.Lease _1 = device.GetReferenceTracker().GetLease();

        device.ThrowIfDeviceLost();

        if (TypeInfo<T>.IsDoubleOrContainsDoubles &&
            device.D3D12Device->CheckFeatureSupport<D3D12_FEATURE_DATA_D3D12_OPTIONS>(D3D12_FEATURE_D3D12_OPTIONS).DoublePrecisionFloatShaderOps == 0)
        {
            UnsupportedDoubleOperationException.Throw<T>();
        }

        GraphicsDevice = device;
        Length = length;

        ulong sizeInBytes = (uint)length * (uint)sizeof(T);

        this.memoryAllocation = device.CreateOrAllocateResource(
            resourceType,
            allocationMode,
            sizeInBytes,
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
    /// Gets the length of the current buffer.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the <see cref="ID3D12Resource"/> instance currently mapped.
    /// </summary>
    internal ID3D12Resource* D3D12Resource => this.d3D12Resource.Get();

    /// <summary>
    /// Gets the pointer to the start of the mapped buffer data.
    /// </summary>
    internal T* MappedData => this.mappedData;

    /// <inheritdoc/>
    /// <remarks>
    /// The returned view is untracked. It stops being valid once the current instance is disposed, and the
    /// runtime does not know it is in use. Hold a native reference with <c>AcquireNativeResource</c> to defer
    /// the release of the mapped memory for the duration of the use.
    /// </remarks>
    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

            return new MemoryManager(this).Memory;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The returned view is untracked. It stops being valid once the current instance is disposed, and the
    /// runtime does not know it is in use. Hold a native reference with <c>AcquireNativeResource</c> to defer
    /// the release of the mapped memory for the duration of the use.
    /// </remarks>
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            using ReferenceTracker.Lease _0 = GetReferenceTracker().GetLease();

            return new(this.mappedData, Length);
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

    /// <summary>
    /// A <see cref="MemoryManager{T}"/> implementation wrapping a <see cref="TransferBuffer{T}"/> instance.
    /// </summary>
    /// <param name="buffer">The <see cref="TransferBuffer{T}"/> in use.</param>
    private sealed class MemoryManager(TransferBuffer<T> buffer) : MemoryManager<T>
    {
        /// <summary>
        /// The <see cref="TransferBuffer{T}"/> in use.
        /// </summary>
        private readonly TransferBuffer<T> buffer = buffer;

        /// <inheritdoc/>
        public override Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CreateMemory(this.buffer.Length);
        }

        /// <inheritdoc/>
        public override Span<T> GetSpan()
        {
            return this.buffer.Span;
        }

        /// <inheritdoc/>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            default(ArgumentOutOfRangeException).ThrowIfNotZero(elementIndex);

            using ReferenceTracker.Lease _0 = this.buffer.GetReferenceTracker().GetLease();

            return new(this.buffer.mappedData);
        }

        /// <inheritdoc/>
        public override void Unpin()
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}