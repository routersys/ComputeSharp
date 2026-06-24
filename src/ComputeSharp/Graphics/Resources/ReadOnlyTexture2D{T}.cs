using System.Diagnostics;
using ComputeSharp.Graphics.Resources.Enums;
using ComputeSharp.Resources;
using ComputeSharp.Resources.Debug;
using ComputeSharp.Win32;
using static ComputeSharp.Win32.D3D12_FORMAT_SUPPORT1;

namespace ComputeSharp;

/// <summary>
/// A <see langword="class"/> representing a typed readonly 2D texture stored on GPU memory.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
[DebuggerTypeProxy(typeof(Texture2DDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
public sealed class ReadOnlyTexture2D<T> : Texture2D<T>
    where T : unmanaged
{
    /// <summary>
    /// Creates a new <see cref="ReadOnlyTexture2D{T}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> associated with the current instance.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <param name="allocationMode">The allocation mode to use for the new resource.</param>
    internal ReadOnlyTexture2D(GraphicsDevice device, int width, int height, AllocationMode allocationMode)
        : base(device, width, height, ResourceType.ReadOnly, allocationMode, D3D12_FORMAT_SUPPORT1_TEXTURE2D)
    {
    }

    /// <summary>
    /// クロス API 共有が可能な <see cref="ReadOnlyTexture2D{T}"/> インスタンスを生成します。
    /// </summary>
    /// <param name="device">現在のインスタンスに関連付ける <see cref="GraphicsDevice"/>。</param>
    /// <param name="width">テクスチャの幅。</param>
    /// <param name="height">テクスチャの高さ。</param>
    internal ReadOnlyTexture2D(GraphicsDevice device, int width, int height)
        : base(device, width, height, ResourceType.ReadOnly, D3D12_FORMAT_SUPPORT1_TEXTURE2D)
    {
    }

    /// <summary>
    /// 外部 API が所有する共有リソースをラップする <see cref="ReadOnlyTexture2D{T}"/> インスタンスを生成します。
    /// </summary>
    /// <param name="device">現在のインスタンスに関連付ける <see cref="GraphicsDevice"/>。</param>
    /// <param name="d3D12Resource">ラップ対象の、共有ハンドルから開かれた <see cref="ID3D12Resource"/>。</param>
    internal unsafe ReadOnlyTexture2D(GraphicsDevice device, ID3D12Resource* d3D12Resource)
        : base(device, d3D12Resource, ResourceType.ReadOnly, D3D12_FORMAT_SUPPORT1_TEXTURE2D)
    {
    }

    /// <summary>
    /// Gets a single <typeparamref name="T"/> value from the current readonly texture.
    /// </summary>
    /// <param name="x">The horizontal offset of the value to get.</param>
    /// <param name="y">The vertical offset of the value to get.</param>
    /// <remarks>This API can only be used from a compute shader, and will always throw if used anywhere else.</remarks>
    public ref readonly T this[int x, int y] => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T>)}[{typeof(int)}, {typeof(int)}]");

    /// <summary>
    /// Gets a single <typeparamref name="T"/> value from the current readonly texture.
    /// </summary>
    /// <param name="xy">The coordinates of the value to get.</param>
    /// <remarks>This API can only be used from a compute shader, and will always throw if used anywhere else.</remarks>
    public ref readonly T this[Int2 xy] => throw new InvalidExecutionContextException($"{typeof(ReadOnlyTexture2D<T>)}[{typeof(Int2)}]");

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"ComputeSharp.ReadOnlyTexture2D<{typeof(T)}>[{Width}, {Height}]";
    }
}