using System.Runtime.CompilerServices;
using ComputeWeave.D2D1.Shaders.Interop.Effects.ResourceManagers;
using ComputeWeave.Win32;

#pragma warning disable IDE0051

namespace ComputeWeave.D2D1.Interop.Effects;

/// <inheritdoc/>
partial struct PixelShaderEffect
{
    /// <summary>
    /// A buffer of 16 <see cref="ID2D1ResourceTextureManager"/> objects.
    /// </summary>
    [InlineArray(16)]
    private unsafe struct ResourceTextureManagerBuffer
    {
        /// <summary>
        /// The <see cref="ID2D1ResourceTextureManager"/> instance at index 0.
        /// </summary>
        private ComPtr<ID2D1ResourceTextureManager> resourceTextureManager0;
    }
}