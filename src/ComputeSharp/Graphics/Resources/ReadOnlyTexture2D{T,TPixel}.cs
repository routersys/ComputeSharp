using System.Diagnostics;
using ComputeSharp.Graphics.Resources.Enums;
using ComputeSharp.Resources;
using ComputeSharp.Resources.Debug;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_FORMAT_SUPPORT1;

#pragma warning disable IDE0022

namespace ComputeSharp;

/// <summary>
/// A <see langword="class"/> representing a typed readonly 2D texture stored on GPU memory.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
/// <typeparam name="TPixel">The type of pixels used on the GPU side.</typeparam>
[DebuggerTypeProxy(typeof(Texture2DDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
public sealed class ReadOnlyTexture2D<T, TPixel> : Texture2D<T>, IReadOnlyNormalizedTexture2D<TPixel>
    where T : unmanaged, IPixel<T, TPixel>
    where TPixel : unmanaged
{
    /// <summary>
    /// Creates a new <see cref="ReadOnlyTexture2D{T, TPixel}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> associated with the current instance.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <param name="allocationMode">The allocation mode to use for the new resource.</param>
    internal ReadOnlyTexture2D(GraphicsDevice device, int width, int height, AllocationMode allocationMode)
        : base(device, width, height, ResourceType.ReadOnly, allocationMode, D3D12_FORMAT_SUPPORT1_TEXTURE2D)
    {
    }

    internal unsafe ReadOnlyTexture2D(GraphicsDevice device, ID3D12Resource* d3D12Resource, int width, int height, D3D12_RESOURCE_STATES d3D12ResourceStates)
        : base(device, d3D12Resource, width, height, ResourceType.ReadOnly, d3D12ResourceStates, D3D12_FORMAT_SUPPORT1_TEXTURE2D)
    {
    }

    /// <inheritdoc/>
    public ref readonly TPixel this[int x, int y] => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T, TPixel>)}[{typeof(int)}, {typeof(int)}]");

    /// <inheritdoc/>
    public ref readonly TPixel this[Int2 xy] => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T, TPixel>)}[{typeof(Int2)}]");

    /// <inheritdoc/>
    public ref readonly TPixel Sample(float u, float v) => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T, TPixel>)}.{nameof(Sample)}({typeof(float)}, {typeof(float)})");

    /// <inheritdoc/>
    public ref readonly TPixel Sample(Float2 uv) => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T, TPixel>)}.{nameof(Sample)}({typeof(Float2)})");

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"ComputeSharp.ReadOnlyTexture2D<{typeof(T)}, {typeof(TPixel)}>[{Width}, {Height}]";
    }
}