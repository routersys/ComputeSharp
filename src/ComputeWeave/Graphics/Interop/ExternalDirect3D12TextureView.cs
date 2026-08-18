using System;
using System.Threading;
using ComputeWeave.Win32;

namespace ComputeWeave;

/// <summary>
/// The external view a <see cref="ComputeExternalDirect3D12Provider"/> creates over a shared texture.
/// </summary>
/// <remarks>
/// <para>
/// The view owns the opened resource, and the shared texture itself stays owned by the graphics device.
/// Releasing the view releases the opened resource.
/// </para>
/// <para>
/// The pointer is exposed so that the caller can record its own work against the resource. <see cref="Resource"/>
/// is borrowed and must not be released. Handing a borrowed pointer to a binding that manages reference counts
/// releases it behind the caller's back, so use <see cref="AddRefResource"/> for that: it returns a reference
/// the caller owns.
/// </para>
/// </remarks>
public sealed unsafe class ExternalDirect3D12TextureView : IDisposable
{
    /// <summary>
    /// The opened <c>ID3D12Resource</c> object.
    /// </summary>
    private nint resource;

    /// <summary>
    /// Creates a new <see cref="ExternalDirect3D12TextureView"/> instance with the specified parameters.
    /// </summary>
    /// <param name="resource">The opened <c>ID3D12Resource</c> object.</param>
    internal ExternalDirect3D12TextureView(ID3D12Resource* resource)
    {
        this.resource = (nint)resource;
    }

    /// <summary>
    /// Gets the opened <c>ID3D12Resource</c> object, or <c>0</c> if the current view has been released.
    /// </summary>
    public nint Resource => Volatile.Read(ref this.resource);

    /// <summary>
    /// Takes a reference on the opened <c>ID3D12Resource</c> object and returns it.
    /// </summary>
    /// <returns>
    /// The object, with one reference the caller owns and releases, or <c>0</c> if the current view has been
    /// released. No reference is taken when <c>0</c> is returned.
    /// </returns>
    /// <remarks>
    /// The returned reference is independent of the one the current view holds. Releasing it leaves the view
    /// usable, and releasing the view leaves the returned reference usable.
    /// </remarks>
    public nint AddRefResource()
    {
        nint resource = Volatile.Read(ref this.resource);

        if (resource == 0)
        {
            return 0;
        }

        _ = ((ID3D12Resource*)resource)->AddRef();

        return resource;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This must not run while another thread is reading the current view. References handed out by
    /// <see cref="AddRefResource"/> stay valid across it.
    /// </remarks>
    public void Dispose()
    {
        nint resource = Interlocked.Exchange(ref this.resource, 0);

        if (resource != 0)
        {
            _ = ((ID3D12Resource*)resource)->Release();
        }
    }
}
