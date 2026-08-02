using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Resources;

namespace ComputeWeave.Interop;

/// <inheritdoc/>
public static unsafe partial class InteropServices
{
    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given buffer.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer.</typeparam>
    /// <param name="buffer">The <see cref="Buffer{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the buffer has no available resource generation.</exception>
    /// <remarks>
    /// The returned reference keeps the resource generation alive until it is disposed. It does not order the
    /// work the caller issues against the work the runtime issues. See <see cref="NativeResourceSynchronization"/>.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        Buffer<T> buffer,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(buffer);

        using ReferenceTracker.Lease _0 = buffer.GraphicsDevice.GetReferenceTracker().GetLease();

        return buffer.GraphicsDevice.AcquireNativeResource(buffer, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="Texture1D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The returned reference keeps the resource generation alive until it is disposed. It does not order the
    /// work the caller issues against the work the runtime issues. See <see cref="NativeResourceSynchronization"/>.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        Texture1D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="Texture2D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The returned reference keeps the resource generation alive until it is disposed. It does not order the
    /// work the caller issues against the work the runtime issues. See <see cref="NativeResourceSynchronization"/>.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        Texture2D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="Texture3D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The returned reference keeps the resource generation alive until it is disposed. It does not order the
    /// work the caller issues against the work the runtime issues. See <see cref="NativeResourceSynchronization"/>.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        Texture3D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given transfer buffer.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer.</typeparam>
    /// <param name="buffer">The <see cref="TransferBuffer{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the buffer has no available resource generation.</exception>
    /// <remarks>
    /// The runtime never leaves work pending on a transfer resource, so the reported completion points are
    /// always empty. The reference defers the release of the mapped memory until it is disposed.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        TransferBuffer<T> buffer,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(buffer);

        using ReferenceTracker.Lease _0 = buffer.GraphicsDevice.GetReferenceTracker().GetLease();

        return buffer.GraphicsDevice.AcquireNativeResource(buffer, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given transfer texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="TransferTexture1D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The runtime never leaves work pending on a transfer resource, so the reported completion points are
    /// always empty. The reference defers the release of the mapped memory until it is disposed.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        TransferTexture1D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given transfer texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="TransferTexture2D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The runtime never leaves work pending on a transfer resource, so the reported completion points are
    /// always empty. The reference defers the release of the mapped memory until it is disposed.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        TransferTexture2D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Acquires a native reference on the resource generation currently backing a given transfer texture.
    /// </summary>
    /// <typeparam name="T">The type of items stored on the texture.</typeparam>
    /// <param name="texture">The <see cref="TransferTexture3D{T}"/> instance to reference.</param>
    /// <param name="synchronization">The completion points of the work already submitted for the generation.</param>
    /// <param name="acquisition">The synchronization to perform while acquiring the reference.</param>
    /// <returns>The resulting <see cref="NativeResourceReference"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="texture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the texture has no available resource generation.</exception>
    /// <remarks>
    /// The runtime never leaves work pending on a transfer resource, so the reported completion points are
    /// always empty. The reference defers the release of the mapped memory until it is disposed.
    /// </remarks>
    public static NativeResourceReference AcquireNativeResource<T>(
        TransferTexture3D<T> texture,
        out NativeResourceSynchronization synchronization,
        NativeResourceAcquisition acquisition = NativeResourceAcquisition.Immediate)
        where T : unmanaged
    {
        default(ArgumentNullException).ThrowIfNull(texture);

        using ReferenceTracker.Lease _0 = texture.GraphicsDevice.GetReferenceTracker().GetLease();

        return texture.GraphicsDevice.AcquireNativeResource(texture, acquisition, out synchronization);
    }

    /// <summary>
    /// Gets the fence of a given queue of a device, as a specified interface.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> instance in use.</param>
    /// <param name="queue">The queue to get the fence of.</param>
    /// <param name="riid">A reference to the interface identifier (IID) of the fence interface being queried for.</param>
    /// <param name="ppvFence">The address of a pointer to an interface with the IID specified in <paramref name="riid"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="queue"/> does not identify a queue.</exception>
    /// <remarks>
    /// The runtime owns the values signaled on the returned fence. Callers must not signal it. The fence is
    /// meant to be waited upon, so that work issued outside the runtime can be ordered after the completion
    /// points reported by <see cref="NativeResourceSynchronization"/>.
    /// </remarks>
    public static void GetID3D12Fence(GraphicsDevice device, ComputeQueueKind queue, Guid* riid, void** ppvFence)
    {
        default(ArgumentNullException).ThrowIfNull(device);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.GetQueueFence(queue)->QueryInterface(riid, ppvFence).Assert();
    }
}
