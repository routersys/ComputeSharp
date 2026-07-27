using System;
using ComputeSharp.Graphics.Helpers;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Win32;

namespace ComputeSharp;

/// <summary>
/// A slot owning the successive generations of a shared texture declared by a compute interop resource set.
/// </summary>
/// <typeparam name="T">The type of items stored on the texture.</typeparam>
/// <typeparam name="TPixel">The type of pixels used on the GPU side.</typeparam>
/// <typeparam name="TView">The type of the external view of the texture.</typeparam>
public sealed class SharedTextureSlot<T, TPixel, TView> : IComputeSharedResourceSlot, IDisposable, IComputeSharedSlot
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
    private SlotGate slotGate;

    /// <summary>
    /// The resource set the current slot is bound to, or <see langword="null"/> if it is not bound.
    /// </summary>
    private InteropResourceSetRuntime? runtime;

    /// <summary>
    /// Creates a new <see cref="SharedTextureSlot{T, TPixel, TView}"/> instance that is not bound to a resource set.
    /// </summary>
    public SharedTextureSlot()
    {
    }

    /// <summary>
    /// Gets the current logical width of the shared texture.
    /// </summary>
    public int Width
    {
        get
        {
            this.slotGate.GetActiveLogicalExtent(out int width, out _);

            return width;
        }
    }

    /// <summary>
    /// Gets the current logical height of the shared texture.
    /// </summary>
    public int Height
    {
        get
        {
            this.slotGate.GetActiveLogicalExtent(out _, out int height);

            return height;
        }
    }

    /// <summary>
    /// Gets whether the current slot owns a published texture generation.
    /// </summary>
    public bool IsAllocated => this.slotGate.IsAllocated;

    /// <summary>
    /// Gets whether disposal of the current slot has been requested.
    /// </summary>
    internal bool IsDisposeRequested => this.slotGate.IsDisposeRequested;

    /// <inheritdoc/>
    bool IComputeSharedSlot.IsDisposalComplete => this.slotGate.IsDisposalComplete;

    /// <inheritdoc/>
    bool IComputeSharedSlot.TryBind(
        InteropResourceSetRuntime runtime,
        int[] planStorage,
        in SlotResourcePlanStateRecord planState)
    {
        default(InvalidOperationException).ThrowIf(
            DXGIFormatHelper.GetForType<T>() != DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            "A shared texture slot only stores the pixel type the shared texture native descriptor is fixed to.");

        this.runtime = runtime;

        if (this.slotGate.TryBind(planStorage, in planState))
        {
            return true;
        }

        this.runtime = null;

        return false;
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.RequestDispose()
    {
        Dispose();
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.RunMaintenance()
    {
        SlotGenerationMaintenance.Run(ref this.slotGate);
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.MarkTerminalRetained()
    {
        _ = this.slotGate.TryMarkDeviceTerminal();
    }

    /// <inheritdoc/>
    void IComputeSharedSlot.ReleaseTerminalGenerations()
    {
        SlotTerminalRelease.Run(ref this.slotGate);
    }

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

        return this.slotGate.TryGetBinding(0, out ComputeResourceBinding<ReadWriteTexture2D<T, TPixel>> binding)
            ? binding
            : default;
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
        PreparedGenerationRollback.RollbackUnpublished(this.slotGate.RequestDispose());

        SlotGenerationMaintenance.Run(ref this.slotGate);
    }

    /// <summary>
    /// Waits for the disposal of the current slot to complete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if disposal of the current slot has not been requested.</exception>
    /// <remarks>
    /// A slot that is not bound to a resource set has nothing to wait for and returns immediately.
    /// </remarks>
    public void WaitForDisposal()
    {
        if (this.runtime is not InteropResourceSetRuntime boundRuntime)
        {
            return;
        }

        SlotDisposalWait.Run(ref this.slotGate, boundRuntime.Registry, "The shared texture slot has not been disposed.");
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
