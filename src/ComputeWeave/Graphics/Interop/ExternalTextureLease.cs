using System;
using System.Threading;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Interop;
using ComputeWeave.Resources.Lifetime;

namespace ComputeWeave;

/// <summary>
/// A persistent lease over the external view of a shared texture generation.
/// </summary>
/// <typeparam name="TView">The type of the external view.</typeparam>
/// <remarks>
/// A lease holds an external reference over its generation and a reference over its domain, but no external
/// queue ownership, so every external queue work over the leased view runs inside the scope of one of the
/// operations the lease hands out. Its <see cref="Width"/> and <see cref="Height"/> are captured together with
/// the leased generation and remain unchanged when the slot publishes another generation.
/// </remarks>
public sealed class ExternalTextureLease<TView> : IDisposable
    where TView : class
{
    /// <summary>
    /// The resource set the leased generation belongs to.
    /// </summary>
    private readonly InteropResourceSetRuntime runtime;

    /// <summary>
    /// The generation the current lease holds an external reference of.
    /// </summary>
    private readonly ResourceGenerationPin pin;

    /// <summary>
    /// The leased external view.
    /// </summary>
    private readonly TView view;

    /// <summary>
    /// The width of the leased texture generation.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// The height of the leased texture generation.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// Whether the current lease has been disposed.
    /// </summary>
    private int isDisposed;

    /// <summary>
    /// Creates a new <see cref="ExternalTextureLease{TView}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="runtime">The resource set the leased generation belongs to.</param>
    /// <param name="pin">The generation the lease holds an external reference of.</param>
    /// <param name="view">The leased external view.</param>
    /// <param name="width">The width of the leased texture generation.</param>
    /// <param name="height">The height of the leased texture generation.</param>
    internal ExternalTextureLease(
        InteropResourceSetRuntime runtime,
        in ResourceGenerationPin pin,
        TView view,
        int width,
        int height)
    {
        this.runtime = runtime;
        this.pin = pin;
        this.view = view;
        this.width = width;
        this.height = height;
    }

    /// <summary>
    /// Gets whether the current lease has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref this.isDisposed) != 0;

    /// <summary>
    /// Gets the width of the leased texture generation.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the current lease has been disposed.</exception>
    public int Width
    {
        get
        {
            default(ObjectDisposedException).ThrowIf(IsDisposed, this);

            return this.width;
        }
    }

    /// <summary>
    /// Gets the height of the leased texture generation.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the current lease has been disposed.</exception>
    public int Height
    {
        get
        {
            default(ObjectDisposedException).ThrowIf(IsDisposed, this);

            return this.height;
        }
    }

    /// <summary>
    /// Gets the leased external view.
    /// </summary>
    /// <returns>The leased external view.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the current lease has been disposed.</exception>
    /// <exception cref="Exception">Rethrown from the failure the domain of the lease or its device was left with.</exception>
    public TView DangerousGetView()
    {
        default(ObjectDisposedException).ThrowIf(IsDisposed, this);

        this.runtime.Domain.ThrowIfPoisonedOrDeviceTerminal();

        return this.view;
    }

    /// <summary>
    /// Begins an external queue operation over the leased external view.
    /// </summary>
    /// <returns>A scoped operation holding the external queue ownership.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the current lease has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the domain or the leased generation cannot take the operation.</exception>
    /// <exception cref="Exception">Rethrown from the failure the domain of the lease or its device was left with.</exception>
    public ExternalQueueOperation BeginExternalQueueOperation()
    {
        default(ObjectDisposedException).ThrowIf(IsDisposed, this);

        ComputeInteropDomain domain = this.runtime.Domain;

        domain.ThrowIfPoisonedOrDeviceTerminal();

        DomainOperationStatus status = domain.TryAcquireOperation(
            ExternalDomainReference.TransientOperation,
            this.pin.GenerationId,
            releaseExternalReferenceOnDispose: false,
            out DomainOperationLease lease,
            out Exception? schedulerFailure);

        if (status is not DomainOperationStatus.Acquired)
        {
            throw new InvalidOperationException(
                $"The external texture view lease could not acquire an operation of its domain ({status}).",
                schedulerFailure);
        }

        ExternalQueueOperation operation = new(domain, lease.Token, this.pin.GenerationId);

        if (operation.IsBoundGenerationAvailable(in this.pin))
        {
            return operation;
        }

        operation.Dispose();

        throw new InvalidOperationException("The leased texture generation is not available to the external queue.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
        {
            return;
        }

        this.runtime.ReleasePersistentLease();
        this.runtime.Domain.ReleaseReference(ExternalDomainReference.PersistentLease);
        this.runtime.Domain.ReleaseGenerationPersistentLease(in this.pin);
    }
}
