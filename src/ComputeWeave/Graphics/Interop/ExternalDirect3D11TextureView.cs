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
/// The pointers are exposed so that the caller can pass the bitmap to its own Direct2D drawing. The caller
/// borrows them for as long as it holds the view, and must not release them.
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

    /// <inheritdoc/>
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
