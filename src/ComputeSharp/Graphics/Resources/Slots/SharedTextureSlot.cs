using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a shared texture declared by a compute interop resource set.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
/// <typeparam name="TPixel">The type of pixels used on the GPU side.</typeparam>
/// <typeparam name="TView">The type of the external view of the texture.</typeparam>
public sealed class SharedTextureSlot<T, TPixel, TView> : IDisposable
    where T : unmanaged, IPixel<T, TPixel>
    where TPixel : unmanaged
    where TView : class, IDisposable
{
    /// <summary>
    /// The message for operations requiring a published texture generation.
    /// </summary>
    private const string NoPublishedGeneration = "The shared texture slot has no published texture generation.";

    /// <summary>
    /// The gate protecting the state of the current slot.
    /// </summary>
    private readonly SlotGate slotGate = new();

    /// <summary>
    /// Creates a new <see cref="SharedTextureSlot{T, TPixel, TView}"/> instance that is not bound to a resource set.
    /// </summary>
    public SharedTextureSlot()
    {
    }

    /// <summary>
    /// Gets the current logical width of the shared texture.
    /// </summary>
    public int Width => 0;

    /// <summary>
    /// Gets the current logical height of the shared texture.
    /// </summary>
    public int Height => 0;

    /// <summary>
    /// Gets whether the current slot owns a published texture generation.
    /// </summary>
    public bool IsAllocated => this.slotGate.IsAllocated;

    /// <summary>
    /// Gets whether disposal of the current slot has been requested.
    /// </summary>
    internal bool IsDisposeRequested => this.slotGate.IsDisposeRequested;

    /// <summary>
    /// Ensures the shared texture matches the requested logical dimensions.
    /// </summary>
    /// <param name="width">The requested logical width.</param>
    /// <param name="height">The requested logical height.</param>
    /// <param name="changed">Whether the published texture generation was replaced.</param>
    /// <returns>Whether the shared texture matches the requested logical dimensions.</returns>
    public bool TryEnsure(int width, int height, out bool changed)
    {
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(width);
        default(ArgumentOutOfRangeException).ThrowIfNegativeOrZero(height);

        changed = false;

        ThrowIfNotBound();

        throw new InvalidOperationException(NoPublishedGeneration);
    }

    /// <summary>
    /// Gets a binding to the currently published texture generation.
    /// </summary>
    /// <returns>A binding to the currently published texture generation.</returns>
    public ComputeResourceBinding<ReadWriteTexture2D<T, TPixel>> GetComputeBinding()
    {
        ThrowIfNotBound();

        throw new InvalidOperationException(NoPublishedGeneration);
    }

    /// <summary>
    /// Begins a transient external operation over the currently published texture generation.
    /// </summary>
    /// <returns>A transient borrow of the external view.</returns>
    public BorrowedExternalTextureView<TView> BeginExternalOperation()
    {
        ThrowIfNotBound();

        throw new InvalidOperationException(NoPublishedGeneration);
    }

    /// <summary>
    /// Acquires a persistent lease over the external view of the currently published texture generation.
    /// </summary>
    /// <returns>A persistent lease over the external view.</returns>
    public ExternalTextureLease<TView> AcquireExternalViewLease()
    {
        ThrowIfNotBound();

        throw new InvalidOperationException(NoPublishedGeneration);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = this.slotGate.RequestDispose();
    }

    /// <summary>
    /// Waits for the disposal of the current slot to complete.
    /// </summary>
    public void WaitForDisposal()
    {
        default(InvalidOperationException).ThrowIf(
            !this.slotGate.IsDisposalComplete,
            "The shared texture slot is still bound to a resource set.");
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if the current slot is not bound to a resource set.
    /// </summary>
    private void ThrowIfNotBound()
    {
        default(InvalidOperationException).ThrowIf(
            this.slotGate.IsUnbound,
            "The shared texture slot is not bound to a resource set.");
    }
}
