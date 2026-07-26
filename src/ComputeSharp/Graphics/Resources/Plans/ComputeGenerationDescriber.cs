using System;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Graphics.Helpers;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;
using ComputeSharp.Win32;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp.Resources.Plans;

internal enum ComputeGenerationDeclarationStatus : byte
{
    Valid = 0,
    CountMismatch = 1,
    ShapeMismatch = 2,
    DimensionMismatch = 3,
    AllocationInfoInvalid = 4,
    SegmentUnmapped = 5,
    PlacementMismatch = 6,
    DeclarationMismatch = 7
}

internal static unsafe class ComputeGenerationDescriber
{
    public static ResourceType GetResourceType(ComputeResourceAccess access)
    {
        return access is ComputeResourceAccess.Read ? ResourceType.ReadOnly : ResourceType.ReadWrite;
    }

    public static ComputeGenerationDeclarationStatus DescribeBuffer<T>(
        GraphicsDevice device,
        ComputeResourceAccess access,
        int length,
        out ComputeGenerationDeclaration declaration)
        where T : unmanaged
    {
        declaration = default;
        declaration.Shape = ComputeGenerationShape.Buffer;
        declaration.Width = length;
        declaration.Height = 1;

        ulong sizeInBytes = checked((ulong)length * (ulong)sizeof(T));

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
        return DescribeTexture2D(device, access, DXGIFormatHelper.GetForType<T>(), width, height, out declaration);
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
