using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_MEMORY_POOL;

namespace ComputeSharp.Memory;

internal static class GraphicsMemorySegments
{
    public static bool TryMapMemoryPool(bool isUma, D3D12_MEMORY_POOL memoryPool, out MemoryPlacement placement)
    {
        placement = default;

        if (isUma)
        {
            if (memoryPool is not D3D12_MEMORY_POOL_L0)
            {
                return false;
            }

            placement = MemoryPlacement.Local;

            return true;
        }

        switch (memoryPool)
        {
            case D3D12_MEMORY_POOL_L0:
                placement = MemoryPlacement.NonLocal;

                return true;
            case D3D12_MEMORY_POOL_L1:
                placement = MemoryPlacement.Local;

                return true;
            default:
                return false;
        }
    }

    public static bool IsSegmentActive(bool isUma, MemoryPlacement placement)
    {
        return placement switch
        {
            MemoryPlacement.Local => true,
            MemoryPlacement.NonLocal => !isUma,
            _ => false
        };
    }
}
