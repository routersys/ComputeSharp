using System;
using ComputeWeave.Graphics.Extensions;
using ComputeWeave.Graphics.Helpers;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Memory;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Win32;
using static ComputeWeave.Win32.D3D12_FORMAT_SUPPORT1;
using ResourceType = ComputeWeave.Graphics.Resources.Enums.ResourceType;

namespace ComputeWeave.Resources.Plans;

internal enum ComputeGenerationDeclarationStatus : byte
{
    Valid = 0,
    CountMismatch = 1,
    ShapeMismatch = 2,
    DimensionMismatch = 3,
    AllocationInfoInvalid = 4,
    SegmentUnmapped = 5,
    PlacementMismatch = 6,
    DeclarationMismatch = 7,
    NativeCreationFailed = 8
}

internal static unsafe class ComputeGenerationDescriber
{
    public static ResourceType GetResourceType(ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.Read ? ResourceType.ReadOnly : ResourceType.ReadWrite;
    }

    public static D3D12_FORMAT_SUPPORT1 GetFormatSupport(ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.Read
            ? D3D12_FORMAT_SUPPORT1_TEXTURE2D
            : D3D12_FORMAT_SUPPORT1_TEXTURE2D | D3D12_FORMAT_SUPPORT1_TYPED_UNORDERED_ACCESS_VIEW;
    }

    public static ComputeResourceAccess GetObservedAccess(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Constant or ResourceType.ReadOnly => ComputeResourceAccess.Read,
            ResourceType.ReadWrite => ComputeResourceAccess.ReadWrite,
            _ => default(ArgumentException).Throw<ComputeResourceAccess>(nameof(resourceType))
        };
    }

    public static TrackedResourceState GetBufferResidentState(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Constant => TrackedResourceState.GenericRead,
            ResourceType.ReadOnly or ResourceType.ReadWrite => TrackedResourceState.Common,
            _ => default(ArgumentException).Throw<TrackedResourceState>(nameof(resourceType))
        };
    }

    public static D3D12_RESOURCE_STATES GetD3D12ResourceStates(TrackedResourceState trackedState)
    {
        return trackedState switch
        {
            TrackedResourceState.Common => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON,
            TrackedResourceState.UnorderedAccess => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
            TrackedResourceState.NonPixelShaderResource => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE,
            TrackedResourceState.CopySource => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_SOURCE,
            TrackedResourceState.CopyDestination or TrackedResourceState.ReadbackCopyDestination => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
            TrackedResourceState.GenericRead => D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
            _ => default(ArgumentException).Throw<D3D12_RESOURCE_STATES>(nameof(trackedState))
        };
    }

    public static TrackedResourceState GetTrackedState(D3D12_RESOURCE_STATES d3D12ResourceStates)
    {
        return d3D12ResourceStates switch
        {
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON => TrackedResourceState.Common,
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS => TrackedResourceState.UnorderedAccess,
            D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE => TrackedResourceState.NonPixelShaderResource,
            _ => default(ArgumentException).Throw<TrackedResourceState>(nameof(d3D12ResourceStates))
        };
    }

    public static ComputeGenerationDeclarationStatus DescribeBuffer<T>(
        GraphicsDevice device,
        ComputeResourceAccess access,
        int length,
        out ComputeGenerationDeclaration declaration)
        where T : unmanaged
    {
        uint elementSizeInBytes = (uint)sizeof(T);

        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(length, 1, (uint.MaxValue / elementSizeInBytes) & ~255);

        declaration = default;
        declaration.Shape = ComputeGenerationShape.Buffer;
        declaration.Width = length;
        declaration.Height = 1;

        ulong sizeInBytes = checked((ulong)length * elementSizeInBytes);

        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetCommittedResourceDescription(
            GetResourceType(access),
            sizeInBytes,
            device.IsCacheCoherentUMA);

        return Complete(device, description, ref declaration);
    }

    public static ComputeGenerationDeclarationStatus DescribeTexture2D<T>(
        GraphicsDevice device,
        ComputeResourceAccess access,
        int width,
        int height,
        out ComputeGenerationDeclaration declaration)
        where T : unmanaged
    {
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(width, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(height, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);

        DXGI_FORMAT dxgiFormat = DXGIFormatHelper.GetForType<T>();

        if (!device.D3D12Device->IsDxgiFormatSupported(dxgiFormat, GetFormatSupport(access)))
        {
            UnsupportedTextureTypeException.ThrowForTexture2D<T>();
        }

        return DescribeTexture2D(device, access, dxgiFormat, width, height, out declaration);
    }

    public static ComputeGenerationDeclarationStatus DescribeTexture2D(
        GraphicsDevice device,
        ComputeResourceAccess access,
        DXGI_FORMAT dxgiFormat,
        int width,
        int height,
        out ComputeGenerationDeclaration declaration)
    {
        declaration = default;
        declaration.Shape = ComputeGenerationShape.Texture2D;
        declaration.Width = width;
        declaration.Height = height;

        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetCommittedResourceDescription(
            GetResourceType(access),
            dxgiFormat,
            (uint)width,
            (uint)height,
            device.IsCacheCoherentUMA);

        return Complete(device, description, ref declaration);
    }

    public static ComputeGenerationDeclarationStatus DescribeInteropSharedTexture(
        GraphicsDevice device,
        int width,
        int height,
        out ComputeGenerationDeclaration declaration)
    {
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(width, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);
        default(ArgumentOutOfRangeException).ThrowIfNotBetweenOrEqual(height, 1, D3D12.D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION);

        if (!device.D3D12Device->IsDxgiFormatSupported(
            DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            GetFormatSupport(ComputeResourceAccess.ReadWrite)))
        {
            UnsupportedTextureTypeException.ThrowForTexture2D<Bgra32>();
        }

        declaration = default;
        declaration.Shape = ComputeGenerationShape.Texture2D;
        declaration.Width = width;
        declaration.Height = height;

        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetInteropSharedTextureDescription(
            (uint)width,
            (uint)height);

        return Complete(device, description, ref declaration);
    }

    public static ComputeGenerationDeclarationStatus ValidateAgainstPlan(
        in OwnedSlotDescriptor slot,
        ReadOnlySpan<int> requestedPlan,
        ReadOnlySpan<ComputeGenerationDeclaration> declarations)
    {
        ReadOnlySpan<ResourcePlanFieldDescriptor> planFields = slot.PlanFields.Span;

        if (requestedPlan.Length != planFields.Length)
        {
            return ComputeGenerationDeclarationStatus.DimensionMismatch;
        }

        for (int i = 0; i < planFields.Length; i++)
        {
            uint resourceIndex = planFields[i].SlotResourceIndex;

            if (resourceIndex >= (uint)declarations.Length)
            {
                return ComputeGenerationDeclarationStatus.CountMismatch;
            }

            ref readonly ComputeGenerationDeclaration declaration = ref declarations[(int)resourceIndex];

            int declaredDimension = planFields[i].DimensionKind switch
            {
                ResourcePlanDimensionKind.Length => declaration.Shape is ComputeGenerationShape.Buffer ? declaration.Width : -1,
                ResourcePlanDimensionKind.Width => declaration.Shape is ComputeGenerationShape.Texture2D ? declaration.Width : -1,
                ResourcePlanDimensionKind.Height => declaration.Shape is ComputeGenerationShape.Texture2D ? declaration.Height : -1,
                _ => -1
            };

            if (declaredDimension < 0)
            {
                return ComputeGenerationDeclarationStatus.ShapeMismatch;
            }

            if (declaredDimension != requestedPlan[i])
            {
                return ComputeGenerationDeclarationStatus.DimensionMismatch;
            }
        }

        return ComputeGenerationDeclarationStatus.Valid;
    }

    public static ComputeGenerationDeclarationStatus ValidatePlacement(
        ReadOnlySpan<ComputeGenerationDeclaration> declarations,
        out MemoryPlacement placement,
        out ulong totalSizeInBytes)
    {
        placement = default;
        totalSizeInBytes = 0;

        if (declarations.IsEmpty)
        {
            return ComputeGenerationDeclarationStatus.CountMismatch;
        }

        placement = declarations[0].Placement;

        for (int i = 0; i < declarations.Length; i++)
        {
            if (declarations[i].Placement != placement)
            {
                placement = default;
                totalSizeInBytes = 0;

                return ComputeGenerationDeclarationStatus.PlacementMismatch;
            }

            ulong total = totalSizeInBytes + declarations[i].SizeInBytes;

            if (total < totalSizeInBytes)
            {
                placement = default;
                totalSizeInBytes = 0;

                return ComputeGenerationDeclarationStatus.AllocationInfoInvalid;
            }

            totalSizeInBytes = total;
        }

        return ComputeGenerationDeclarationStatus.Valid;
    }

    private static ComputeGenerationDeclarationStatus Complete(
        GraphicsDevice device,
        in GraphicsCommittedResourceDescription description,
        ref ComputeGenerationDeclaration declaration)
    {
        D3D12_RESOURCE_ALLOCATION_INFO d3D12ResourceAllocationInfo = device.D3D12Device->GetResourceAllocationInfo(in description);

        if (GraphicsAllocationInfo.Validate(d3D12ResourceAllocationInfo.SizeInBytes, d3D12ResourceAllocationInfo.Alignment)
            is not GraphicsAllocationInfoStatus.Valid)
        {
            return ComputeGenerationDeclarationStatus.AllocationInfoInvalid;
        }

        if (!device.TryGetMemoryPlacement(description.HeapProperties, out MemoryPlacement placement))
        {
            return ComputeGenerationDeclarationStatus.SegmentUnmapped;
        }

        declaration.Placement = placement;
        declaration.SizeInBytes = d3D12ResourceAllocationInfo.SizeInBytes;
        declaration.Description = description;

        return ComputeGenerationDeclarationStatus.Valid;
    }
}
