using System;
using System.Threading;
using ComputeWeave.Win32;

namespace ComputeWeave;

/// <summary>
/// The external view a <see cref="ComputeExternalDirect3D11Provider"/> creates over a shared texture.
/// </summary>
/// <remarks>
/// <para>
/// The view owns the opened texture, and the bitmap created over it when the provider was given a render
/// target. Releasing the view releases both, and the shared texture itself stays owned by the graphics device.
/// </para>
/// <para>
/// The pointers are exposed so that the caller can pass the bitmap to its own Direct2D drawing. <see cref="Texture"/>
/// and <see cref="Bitmap"/> are borrowed and must not be released. Handing a borrowed pointer to a binding that
/// manages reference counts releases it behind the caller's back, so use <see cref="AddRefBitmap"/> or
/// <see cref="AddRefTexture"/> for that: they return a reference the caller owns.
/// </para>
/// </remarks>
public sealed unsafe class ExternalDirect3D11TextureView : IDisposable
{
    /// <summary>
    /// The opened <c>ID3D11Texture2D</c> object.
    /// </summary>
    private nint texture;

    /// <summary>
    /// The <c>ID2D1Bitmap1</c> object created over <see cref="texture"/>, if any.
    /// </summary>
    private nint bitmap;

    /// <summary>
    /// Creates a new <see cref="ExternalDirect3D11TextureView"/> instance with the specified parameters.
    /// </summary>
    /// <param name="texture">The opened <c>ID3D11Texture2D</c> object.</param>
    /// <param name="bitmap">The <c>ID2D1Bitmap1</c> object created over <paramref name="texture"/>, if any.</param>
    internal ExternalDirect3D11TextureView(ID3D11Texture2D* texture, ID2D1Bitmap1* bitmap)
    {
        this.texture = (nint)texture;
        this.bitmap = (nint)bitmap;
    }

    /// <summary>
    /// Gets the opened <c>ID3D11Texture2D</c> object, or <c>0</c> if the current view has been released.
    /// </summary>
    public nint Texture => Volatile.Read(ref this.texture);

    /// <summary>
    /// Gets the <c>ID2D1Bitmap1</c> object created over <see cref="Texture"/>.
    /// </summary>
    /// <remarks>
    /// This is <c>0</c> when the provider that created the current view was given no render target, and after
    /// the current view has been released.
    /// </remarks>
    public nint Bitmap => Volatile.Read(ref this.bitmap);

    /// <summary>
    /// Takes a reference on the opened <c>ID3D11Texture2D</c> object and returns it.
    /// </summary>
    /// <returns>
    /// The object, with one reference the caller owns and releases, or <c>0</c> if the current view has been
    /// released. No reference is taken when <c>0</c> is returned.
    /// </returns>
    /// <remarks>
    /// The returned reference is independent of the one the current view holds. Releasing it leaves the view
    /// usable, and releasing the view leaves the returned reference usable.
    /// </remarks>
    public nint AddRefTexture()
    {
        nint texture = Volatile.Read(ref this.texture);

        if (texture == 0)
        {
            return 0;
        }

        _ = ((ID3D11Texture2D*)texture)->AddRef();

        return texture;
    }

    /// <summary>
    /// Takes a reference on the <c>ID2D1Bitmap1</c> object and returns it.
    /// </summary>
    /// <returns>
    /// The object, with one reference the caller owns and releases, or <c>0</c> if the current view carries no
    /// bitmap or has been released. No reference is taken when <c>0</c> is returned.
    /// </returns>
    /// <remarks>
    /// The returned reference is independent of the one the current view holds. Releasing it leaves the view
    /// usable, and releasing the view leaves the returned reference usable.
    /// </remarks>
    public nint AddRefBitmap()
    {
        nint bitmap = Volatile.Read(ref this.bitmap);

        if (bitmap == 0)
        {
            return 0;
        }

        _ = ((ID2D1Bitmap1*)bitmap)->AddRef();

        return bitmap;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This must not run while another thread is reading the current view. References handed out by
    /// <see cref="AddRefTexture"/> and <see cref="AddRefBitmap"/> stay valid across it.
    /// </remarks>
    public void Dispose()
    {
        nint bitmap = Interlocked.Exchange(ref this.bitmap, 0);

        if (bitmap != 0)
        {
            _ = ((ID2D1Bitmap1*)bitmap)->Release();
        }

        nint texture = Interlocked.Exchange(ref this.texture, 0);

        if (texture != 0)
        {
            _ = ((ID3D11Texture2D*)texture)->Release();
        }
    }
}
