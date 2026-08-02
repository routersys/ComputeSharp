using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Win32;

namespace ComputeWeave.Interop;

/// <summary>
/// A reference keeping a device alive while its native object is used outside the runtime.
/// </summary>
/// <remarks>
/// <para>
/// This type must always be used in a <see langword="using"/> statement and disposed properly. Not doing so is
/// undefined behavior and retains the device for the lifetime of the process. Copying an instance and disposing
/// more than one of the copies is undefined behavior as well.
/// </para>
/// <para>
/// Holding a reference defers the release of the native objects a device owns. It does not reject a
/// <see cref="GraphicsDevice.Dispose"/> call, and it does not keep the device usable: a disposed device rejects
/// new work as usual. What it guarantees is that the object obtained here stays valid until the reference is released.
/// </para>
/// </remarks>
public unsafe struct NativeDeviceReference : IDisposable
{
    /// <summary>
    /// The <see cref="GraphicsDevice"/> instance being referenced, or <see langword="null"/> once released.
    /// </summary>
    private GraphicsDevice? device;

    /// <summary>
    /// The <see cref="ID3D12Device"/> object the current reference holds, or <see langword="null"/> once released.
    /// </summary>
    private ID3D12Device* d3D12Device;

    /// <summary>
    /// The lease deferring the native release driven by the reference tracker of the device.
    /// </summary>
    private ReferenceTracker.Lease lease;

    /// <summary>
    /// Creates a new <see cref="NativeDeviceReference"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> instance being referenced.</param>
    /// <param name="d3D12Device">The <see cref="ID3D12Device"/> object the reference takes ownership of.</param>
    /// <param name="lease">The lease of the device the reference takes ownership of.</param>
    internal NativeDeviceReference(GraphicsDevice device, ID3D12Device* d3D12Device, ReferenceTracker.Lease lease)
    {
        this.device = device;
        this.d3D12Device = d3D12Device;
        this.lease = lease;
    }

    /// <summary>
    /// Gets whether the current reference still holds the device it was acquired for.
    /// </summary>
    public readonly bool IsValid => this.device is not null;

    /// <summary>
    /// Gets the underlying COM object of the referenced device, as a specified interface. This method invokes
    /// <see href="https://docs.microsoft.com/windows/win32/api/unknwn/nf-unknwn-iunknown-queryinterface(refiid_void)">IUnknown::QueryInterface</see>.
    /// </summary>
    /// <param name="riid">A reference to the interface identifier (IID) of the device interface being queried for.</param>
    /// <param name="ppvObject">The address of a pointer to an interface with the IID specified in <paramref name="riid"/>.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the current reference has been released.</exception>
    public readonly void QueryInterface(Guid* riid, void** ppvObject)
    {
        default(ObjectDisposedException).ThrowIfNull(this.device);

        this.d3D12Device->QueryInterface(riid, ppvObject).Assert();
    }

    /// <summary>
    /// Tries to get the underlying COM object of the referenced device, as a specified interface.
    /// </summary>
    /// <param name="riid">A reference to the interface identifier (IID) of the device interface being queried for.</param>
    /// <param name="ppvObject">The address of a pointer to an interface with the IID specified in <paramref name="riid"/>.</param>
    /// <returns>
    /// <c>S_OK</c> if the interface is supported, and <c>E_NOINTERFACE</c> otherwise.
    /// If the current reference has been released, then this method returns <c>E_FAIL</c>.
    /// </returns>
    public readonly int TryQueryInterface(Guid* riid, void** ppvObject)
    {
        if (this.device is null)
        {
            return E.E_FAIL;
        }

        return this.d3D12Device->QueryInterface(riid, ppvObject);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GraphicsDevice? device = this.device;
        ID3D12Device* d3D12Device = this.d3D12Device;

        this.device = null;
        this.d3D12Device = null;

        if (device is null)
        {
            return;
        }

        if (d3D12Device is not null)
        {
            _ = d3D12Device->Release();
        }

        this.lease.Dispose();
    }
}
