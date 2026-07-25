using System;

namespace ComputeSharp;

/// <summary>
/// A persistent lease over the external view of a shared texture generation.
/// </summary>
/// <typeparam name="TView">The type of the external view.</typeparam>
public sealed class ExternalTextureLease<TView> : IDisposable
    where TView : class
{
    /// <summary>
    /// The leased external view.
    /// </summary>
    private readonly TView view;

    /// <summary>
    /// Whether the current lease has been disposed.
    /// </summary>
    private volatile bool isDisposed;

    /// <summary>
    /// Creates a new <see cref="ExternalTextureLease{TView}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="view">The leased external view.</param>
    internal ExternalTextureLease(TView view)
    {
        this.view = view;
    }

    /// <summary>
    /// Gets whether the current lease has been disposed.
    /// </summary>
    public bool IsDisposed => this.isDisposed;

    /// <summary>
    /// Gets the leased external view.
    /// </summary>
    /// <returns>The leased external view.</returns>
    public TView DangerousGetView()
    {
        default(InvalidOperationException).ThrowIf(this.isDisposed, "The external texture view lease has been disposed.");

        return this.view;
    }

    /// <summary>
    /// Begins an external queue operation over the leased external view.
    /// </summary>
    /// <returns>A scoped operation holding the external queue ownership.</returns>
    public ExternalQueueOperation BeginExternalQueueOperation()
    {
        default(InvalidOperationException).ThrowIf(this.isDisposed, "The external texture view lease has been disposed.");

        return default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.isDisposed = true;
    }
}
