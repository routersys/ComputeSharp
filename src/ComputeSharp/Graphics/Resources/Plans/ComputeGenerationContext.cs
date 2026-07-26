using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Interop;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Win32;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp;

/// <summary>
/// The context a <see cref="IComputeGenerationMaterializer"/> declares the resources of a generation into.
/// </summary>
public unsafe ref struct ComputeGenerationContext
{
    /// <summary>
    /// The device the declared resources belong to.
    /// </summary>
    private readonly GraphicsDevice device;

    /// <summary>
    /// The allocation descriptors of the slot being materialized.
    /// </summary>
    private readonly Span<ComputeGenerationDeclaration> declarations;

    /// <summary>
    /// The access contracts of the slot being materialized.
    /// </summary>
    private readonly ReadOnlySpan<ComputeResourceAccess> accesses;

    /// <summary>
    /// The generation the declared resources are created into, or <see langword="null"/> while describing.
    /// </summary>
    private readonly ResourceGenerationOwner? owner;

    /// <summary>
    /// The number of declarations completed so far.
    /// </summary>
    private int index;

    /// <summary>
    /// The status of the declarations completed so far.
    /// </summary>
    private ComputeGenerationDeclarationStatus status;

    /// <summary>
    /// The classified outcome of the failed native creation, if any.
    /// </summary>
    private NativeAllocationOutcome outcome;

    /// <summary>
    /// The <see cref="HRESULT"/> of the failed native creation, if any.
    /// </summary>
    private HRESULT hresult;

    /// <summary>
    /// Creates a new <see cref="ComputeGenerationContext"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The device the declared resources belong to.</param>
    /// <param name="declarations">The allocation descriptors of the slot being materialized.</param>
    /// <param name="accesses">The access contracts of the slot being materialized.</param>
    /// <param name="owner">The generation the declared resources are created into, or <see langword="null"/> while describing.</param>
    internal ComputeGenerationContext(
        GraphicsDevice device,
        Span<ComputeGenerationDeclaration> declarations,
        ReadOnlySpan<ComputeResourceAccess> accesses,
        ResourceGenerationOwner? owner)
    {
        this.device = device;
        this.declarations = declarations;
        this.accesses = accesses;
        this.owner = owner;
    }

    /// <summary>
    /// Gets the number of declarations completed so far.
    /// </summary>
    internal readonly int DeclarationCount => this.index;

    /// <summary>
    /// Gets the status of the declarations completed so far.
    /// </summary>
    internal readonly ComputeGenerationDeclarationStatus Status => this.status;

    /// <summary>
    /// Gets the classified outcome of the failed native creation, if any.
    /// </summary>
    internal readonly NativeAllocationOutcome Outcome => this.outcome;

    /// <summary>
    /// Gets the <see cref="HRESULT"/> of the failed native creation, if any.
    /// </summary>
    internal readonly HRESULT NativeResult => this.hresult;

    /// <summary>
    /// Declares a buffer resource of the generation being materialized.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer.</typeparam>
    /// <param name="length">The number of items to store in the buffer.</param>
    public void DeclareBuffer<T>(int length)
        where T : unmanaged
    {
        if (!TryBeginDeclaration(out int resourceOrdinal, out ComputeResourceAccess access))
        {
            return;
        }

        if (this.owner is null)
        {
            CompleteDescribe(
                resourceOrdinal,
                ComputeGenerationDescriber.DescribeBuffer<T>(this.device, access, length, out this.declarations[resourceOrdinal]));

            return;
        }

        if (!TryCreateNativeResource(resourceOrdinal, ComputeGenerationShape.Buffer, length, 1, out ComPtr<ID3D12Resource> d3D12Resource))
        {
            return;
        }

        using ComPtr<ID3D12Resource> lease = d3D12Resource.Move();

        IReferenceTrackedObject resource = ComputeGenerationDescriber.GetResourceType(access) is ResourceType.ReadOnly
            ? new ReadOnlyBuffer<T>(this.device, lease.Get(), length)
            : new ReadWriteBuffer<T>(this.device, lease.Get(), length);

        CompleteCreate(resourceOrdinal, resource, lease.Get());
    }

    /// <summary>
    /// Declares a 2D texture resource of the generation being materialized.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    public void DeclareTexture2D<T>(int width, int height)
        where T : unmanaged
    {
        if (!TryBeginDeclaration(out int resourceOrdinal, out ComputeResourceAccess access))
        {
            return;
        }

        if (this.owner is null)
        {
            CompleteDescribe(
                resourceOrdinal,
                ComputeGenerationDescriber.DescribeTexture2D<T>(this.device, access, width, height, out this.declarations[resourceOrdinal]));

            return;
        }

        if (!TryCreateNativeResource(resourceOrdinal, ComputeGenerationShape.Texture2D, width, height, out ComPtr<ID3D12Resource> d3D12Resource))
        {
            return;
        }

        using ComPtr<ID3D12Resource> lease = d3D12Resource.Move();

        D3D12_RESOURCE_STATES d3D12ResourceStates = this.declarations[resourceOrdinal].Description.ResourceStates;

        IReferenceTrackedObject resource = ComputeGenerationDescriber.GetResourceType(access) is ResourceType.ReadOnly
            ? new ReadOnlyTexture2D<T>(this.device, lease.Get(), width, height, d3D12ResourceStates)
            : new ReadWriteTexture2D<T>(this.device, lease.Get(), width, height, d3D12ResourceStates);

        CompleteCreate(resourceOrdinal, resource, lease.Get());
    }

    /// <summary>
    /// Declares a normalized 2D texture resource of the generation being materialized.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <typeparam name="TPixel">The type of pixels used on the GPU side.</typeparam>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    public void DeclareTexture2D<T, TPixel>(int width, int height)
        where T : unmanaged, IPixel<T, TPixel>
        where TPixel : unmanaged
    {
        if (!TryBeginDeclaration(out int resourceOrdinal, out ComputeResourceAccess access))
        {
            return;
        }

        if (this.owner is null)
        {
            CompleteDescribe(
                resourceOrdinal,
                ComputeGenerationDescriber.DescribeTexture2D<T>(this.device, access, width, height, out this.declarations[resourceOrdinal]));

            return;
        }

        if (!TryCreateNativeResource(resourceOrdinal, ComputeGenerationShape.Texture2D, width, height, out ComPtr<ID3D12Resource> d3D12Resource))
        {
            return;
        }

        using ComPtr<ID3D12Resource> lease = d3D12Resource.Move();

        D3D12_RESOURCE_STATES d3D12ResourceStates = this.declarations[resourceOrdinal].Description.ResourceStates;

        IReferenceTrackedObject resource = ComputeGenerationDescriber.GetResourceType(access) is ResourceType.ReadOnly
            ? new ReadOnlyTexture2D<T, TPixel>(this.device, lease.Get(), width, height, d3D12ResourceStates)
            : new ReadWriteTexture2D<T, TPixel>(this.device, lease.Get(), width, height, d3D12ResourceStates);

        CompleteCreate(resourceOrdinal, resource, lease.Get());
    }

    /// <summary>
    /// Begins the declaration of the next resource of the generation being materialized.
    /// </summary>
    /// <param name="resourceOrdinal">The ordinal of the resource being declared.</param>
    /// <param name="access">The access contract of the resource being declared.</param>
    /// <returns>Whether the declaration can proceed.</returns>
    private bool TryBeginDeclaration(out int resourceOrdinal, out ComputeResourceAccess access)
    {
        resourceOrdinal = this.index;
        access = default;

        if (this.status is not ComputeGenerationDeclarationStatus.Valid)
        {
            return false;
        }

        if ((uint)resourceOrdinal >= (uint)this.declarations.Length)
        {
            this.status = ComputeGenerationDeclarationStatus.CountMismatch;

            return false;
        }

        access = this.accesses[resourceOrdinal];

        return true;
    }

    /// <summary>
    /// Completes the description of a single resource of the generation being materialized.
    /// </summary>
    /// <param name="resourceOrdinal">The ordinal of the described resource.</param>
    /// <param name="status">The status of the completed description.</param>
    private void CompleteDescribe(int resourceOrdinal, ComputeGenerationDeclarationStatus status)
    {
        if (status is not ComputeGenerationDeclarationStatus.Valid)
        {
            this.status = status;

            return;
        }

        this.index = resourceOrdinal + 1;
    }

    /// <summary>
    /// Creates the native resource described for a single resource of the generation being materialized.
    /// </summary>
    /// <param name="resourceOrdinal">The ordinal of the resource being created.</param>
    /// <param name="shape">The declared shape of the resource being created.</param>
    /// <param name="width">The first declared dimension of the resource being created.</param>
    /// <param name="height">The second declared dimension of the resource being created.</param>
    /// <param name="d3D12Resource">The resulting <see cref="ID3D12Resource"/> object.</param>
    /// <returns>Whether the native resource was created.</returns>
    private bool TryCreateNativeResource(
        int resourceOrdinal,
        ComputeGenerationShape shape,
        int width,
        int height,
        out ComPtr<ID3D12Resource> d3D12Resource)
    {
        d3D12Resource = default;

        ComputeGenerationDeclaration declared = default;

        declared.Shape = shape;
        declared.Width = width;
        declared.Height = height;

        if (!this.declarations[resourceOrdinal].IsSameDeclaration(in declared))
        {
            this.status = ComputeGenerationDeclarationStatus.DeclarationMismatch;

            return false;
        }

        HRESULT hresult = this.device.TryCreateCommittedResource(in this.declarations[resourceOrdinal].Description, out d3D12Resource);

        if (hresult >= 0)
        {
            return true;
        }

        if (MemoryAllocationCoordinator.ClassifyNativeResult(hresult) is NativeAllocationOutcome.OutOfMemory)
        {
            _ = this.device.RefreshMemoryObservations();

            hresult = this.device.TryCreateCommittedResource(in this.declarations[resourceOrdinal].Description, out d3D12Resource);

            if (hresult >= 0)
            {
                return true;
            }
        }

        this.status = ComputeGenerationDeclarationStatus.NativeCreationFailed;
        this.outcome = MemoryAllocationCoordinator.ClassifyNativeResult(hresult);
        this.hresult = hresult;

        return false;
    }

    /// <summary>
    /// Completes the creation of a single resource of the generation being materialized.
    /// </summary>
    /// <param name="resourceOrdinal">The ordinal of the created resource.</param>
    /// <param name="resource">The managed object wrapping the created resource.</param>
    /// <param name="d3D12Resource">The <see cref="ID3D12Resource"/> object of the created resource.</param>
    private void CompleteCreate(int resourceOrdinal, IReferenceTrackedObject resource, ID3D12Resource* d3D12Resource)
    {
        this.owner!.AttachResource(
            resource,
            d3D12Resource,
            ComputeGenerationDescriber.GetTrackedState(this.declarations[resourceOrdinal].Description.ResourceStates),
            this.declarations[resourceOrdinal].SizeInBytes);

        this.index = resourceOrdinal + 1;
    }
}
