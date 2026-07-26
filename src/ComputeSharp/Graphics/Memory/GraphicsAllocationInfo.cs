using System;

namespace ComputeSharp.Memory;

internal enum GraphicsAllocationInfoStatus : byte
{
    Valid = 0,
    ApiError = 1,
    UnsupportedPlan = 2
}

internal static class GraphicsAllocationInfo
{
    private const ulong SmallAlignment = 4096;

    private const ulong DefaultAlignment = 65536;

    private const ulong MultisampleAlignment = 4194304;

    public static GraphicsAllocationInfoStatus Validate(ulong sizeInBytes, ulong alignment)
    {
        if (sizeInBytes == ulong.MaxValue)
        {
            return GraphicsAllocationInfoStatus.ApiError;
        }

        if (alignment is not (SmallAlignment or DefaultAlignment or MultisampleAlignment))
        {
            return GraphicsAllocationInfoStatus.UnsupportedPlan;
        }

        return GraphicsAllocationInfoStatus.Valid;
    }

    public static GraphicsAllocationInfoStatus TrySum(
        ReadOnlySpan<ulong> memberSizes,
        ReadOnlySpan<ulong> memberAlignments,
        out ulong totalSizeInBytes)
    {
        default(ArgumentException).ThrowIf(memberSizes.Length != memberAlignments.Length, nameof(memberAlignments));

        totalSizeInBytes = 0;

        for (int i = 0; i < memberSizes.Length; i++)
        {
            GraphicsAllocationInfoStatus status = Validate(memberSizes[i], memberAlignments[i]);

            if (status is not GraphicsAllocationInfoStatus.Valid)
            {
                totalSizeInBytes = 0;

                return status;
            }

            try
            {
                totalSizeInBytes = checked(totalSizeInBytes + memberSizes[i]);
            }
            catch (OverflowException)
            {
                totalSizeInBytes = 0;

                return GraphicsAllocationInfoStatus.UnsupportedPlan;
            }
        }

        return GraphicsAllocationInfoStatus.Valid;
    }
}
