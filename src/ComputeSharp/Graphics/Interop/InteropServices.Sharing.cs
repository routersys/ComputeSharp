using System;
using ComputeSharp.Core.Extensions;
using ComputeSharp.Resources;
using ComputeSharp.Win32;

namespace ComputeSharp.Interop;

/// <inheritdoc/>
public static unsafe partial class InteropServices
{
    /// <summary>
    /// Allocates a <see cref="ReadWriteTexture2D{T}"/> that can be shared across APIs.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to allocate the texture.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <returns>A shareable <see cref="ReadWriteTexture2D{T}"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The returned texture can export a shared NT handle via <see cref="CreateSharedHandle{T}(Texture2D{T})"/>,
    /// which can then be opened from compatible external APIs. Use the normalized overloads for D3D11 and Direct2D shared targets.
    /// Use a shared fence for cross API synchronization.
    /// </remarks>
    public static ReadWriteTexture2D<T> AllocateSharedReadWriteTexture2D<T>(GraphicsDevice device, int width, int height)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        return new ReadWriteTexture2D<T>(device, width, height);
    }

    /// <summary>
    /// Allocates a normalized <see cref="ReadWriteTexture2D{T, TPixel}"/> that can be shared across APIs.
    /// </summary>
    /// <typeparam name="T">The type of pixels used on the CPU side.</typeparam>
    /// <typeparam name="TPixel">The type of normalized pixels used on the GPU side.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to allocate the texture.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <returns>A shareable normalized <see cref="ReadWriteTexture2D{T, TPixel}"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The returned texture supports UAV, simultaneous access and render target usage. It can be opened as a shared resource from D3D11,
    /// and as a Direct2D shared target for compatible formats.
    /// Use a shared fence for cross API synchronization.
    /// </remarks>
    public static ReadWriteTexture2D<T, TPixel> AllocateSharedReadWriteTexture2D<T, TPixel>(GraphicsDevice device, int width, int height)
        where T : unmanaged, IPixel<T, TPixel>
        where TPixel : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        return new ReadWriteTexture2D<T, TPixel>(device, width, height);
    }

    /// <summary>
    /// Allocates a <see cref="ReadOnlyTexture2D{T}"/> that can be shared across APIs.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to allocate the texture.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <returns>A shareable <see cref="ReadOnlyTexture2D{T}"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    public static ReadOnlyTexture2D<T> AllocateSharedReadOnlyTexture2D<T>(GraphicsDevice device, int width, int height)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        return new ReadOnlyTexture2D<T>(device, width, height);
    }

    /// <summary>
    /// Opens a shared NT handle owned by an external API as a <see cref="ReadWriteTexture2D{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to open the resource.</param>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <returns>A <see cref="ReadWriteTexture2D{T}"/> instance wrapping the shared resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the shared resource is not a 2D texture, or if its format does not match <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// To use the imported resource for writing as a <see cref="ReadWriteTexture2D{T}"/>, the original resource must have been
    /// created with UAV support and simultaneous access. Use a shared fence for cross API synchronization.
    /// </remarks>
    public static ReadWriteTexture2D<T> OpenSharedReadWriteTexture2D<T>(GraphicsDevice device, nint handle)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Resource> d3D12Resource = device.OpenSharedResource(new HANDLE((void*)handle));

        return new ReadWriteTexture2D<T>(device, d3D12Resource.Get());
    }

    /// <summary>
    /// Opens a shared NT handle owned by an external API as a normalized <see cref="ReadWriteTexture2D{T, TPixel}"/>.
    /// </summary>
    /// <typeparam name="T">The type of pixels used on the CPU side.</typeparam>
    /// <typeparam name="TPixel">The type of normalized pixels used on the GPU side.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to open the resource.</param>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <returns>A normalized <see cref="ReadWriteTexture2D{T, TPixel}"/> instance wrapping the shared resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the shared resource is not a 2D texture, or if its format does not match <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// To use the resource for writing, the original resource must have been created with UAV support and simultaneous access.
    /// Use a shared fence for cross API synchronization.
    /// </remarks>
    public static ReadWriteTexture2D<T, TPixel> OpenSharedReadWriteTexture2D<T, TPixel>(GraphicsDevice device, nint handle)
        where T : unmanaged, IPixel<T, TPixel>
        where TPixel : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Resource> d3D12Resource = device.OpenSharedResource(new HANDLE((void*)handle));

        return new ReadWriteTexture2D<T, TPixel>(device, d3D12Resource.Get());
    }

    /// <summary>
    /// Opens a shared NT handle owned by an external API as a <see cref="ReadOnlyTexture2D{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to open the resource.</param>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <returns>A <see cref="ReadOnlyTexture2D{T}"/> instance wrapping the shared resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the shared resource is not a 2D texture, or if its format does not match <typeparamref name="T"/>.</exception>
    public static ReadOnlyTexture2D<T> OpenSharedReadOnlyTexture2D<T>(GraphicsDevice device, nint handle)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Resource> d3D12Resource = device.OpenSharedResource(new HANDLE((void*)handle));

        return new ReadOnlyTexture2D<T>(device, d3D12Resource.Get());
    }

    /// <summary>
    /// Exports a shared NT handle for the <see cref="ID3D12Resource"/> backing a shareable texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="Texture2D{T}"/> to export the shared handle for.</param>
    /// <returns>The exported shared NT handle. The caller is responsible for releasing it with <c>CloseHandle</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This method only succeeds for textures created as shareable, such as with
    /// <see cref="AllocateSharedReadWriteTexture2D{T}(GraphicsDevice, int, int)"/>. It fails for textures not created for sharing.
    /// </remarks>
    public static nint CreateSharedHandle<T>(Texture2D<T> texture)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GetReferenceTracker().GetLease();

        HANDLE handle = texture.GraphicsDevice.CreateSharedHandle((IUnknown*)texture.D3D12Resource);

        return (nint)handle.Value;
    }

    /// <summary>
    /// Creates a shared fence to use for cross API synchronization, and retrieves it as the requested interface.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to create the fence.</param>
    /// <param name="riid">A reference to the identifier (IID) of the fence interface to retrieve.</param>
    /// <param name="ppvFence">The address of a pointer to the interface specified by <paramref name="riid"/>.</param>
    /// <param name="sharedHandle">The address to write the shared NT handle of the created fence to. The caller is responsible for releasing it with <c>CloseHandle</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if creating the fence, exporting the handle, or retrieving the interface fails.</exception>
    /// <remarks>
    /// The exported <paramref name="sharedHandle"/> can be opened on the D3D11 side via <c>ID3D11Device5::OpenSharedFence</c>.
    /// The retrieved <paramref name="ppvFence"/> can be passed to <see cref="SignalSharedFence(GraphicsDevice, void*, ulong)"/> and
    /// <see cref="WaitForSharedFence(GraphicsDevice, void*, ulong)"/> to synchronize on the compute queue.
    /// </remarks>
    public static void CreateSharedFence(GraphicsDevice device, Guid* riid, void** ppvFence, nint* sharedHandle)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Fence> d3D12Fence = device.CreateSharedFence();

        HANDLE handle = device.CreateSharedHandle((IUnknown*)d3D12Fence.Get());

        *sharedHandle = (nint)handle.Value;

        d3D12Fence.Get()->QueryInterface(riid, ppvFence).Assert();
    }

    /// <summary>
    /// Opens a shared fence created by another API from a shared NT handle, and retrieves it as the requested interface.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> to use to open the fence.</param>
    /// <param name="handle">The shared NT handle to open.</param>
    /// <param name="riid">A reference to the identifier (IID) of the fence interface to retrieve.</param>
    /// <param name="ppvFence">The address of a pointer to the interface specified by <paramref name="riid"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if opening the fence or retrieving the interface fails.</exception>
    public static void OpenSharedFence(GraphicsDevice device, nint handle, Guid* riid, void** ppvFence)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();
        using ComPtr<ID3D12Fence> d3D12Fence = device.OpenSharedFence(new HANDLE((void*)handle));

        d3D12Fence.Get()->QueryInterface(riid, ppvFence).Assert();
    }

    /// <summary>
    /// Signals the specified shared fence with a value on the compute queue.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> issuing the operation.</param>
    /// <param name="d3D12Fence">A pointer to the target <c>ID3D12Fence</c> COM object.</param>
    /// <param name="value">The value to signal.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if issuing the signal fails.</exception>
    public static void SignalSharedFence(GraphicsDevice device, void* d3D12Fence, ulong value)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.SignalSharedFence((ID3D12Fence*)d3D12Fence, value);
    }

    /// <summary>
    /// Enqueues a wait on the compute queue until the specified shared fence reaches the target value.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> issuing the operation.</param>
    /// <param name="d3D12Fence">A pointer to the target <c>ID3D12Fence</c> COM object.</param>
    /// <param name="value">The target value to wait for.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if enqueueing the wait fails.</exception>
    public static void WaitForSharedFence(GraphicsDevice device, void* d3D12Fence, ulong value)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.WaitForSharedFence((ID3D12Fence*)d3D12Fence, value);
    }
}
