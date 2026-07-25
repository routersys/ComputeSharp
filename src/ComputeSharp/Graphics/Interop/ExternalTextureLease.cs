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
    /// Whether the current lease has been disposed.
    /// </summary>
    private volatile bool isDisposed;

    /// <summary>
    /// Creates a new <see cref="ExternalTextureLease{TView}"/> instance.
    /// </summary>
    private ExternalTextureLease()
    {
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
        throw new InvalidOperationException("The external texture view lease holds no external view.");
    }

    /// <summary>
    /// Begins an external queue operation over the leased external view.
    /// </summary>
    /// <returns>A scoped operation holding the external queue ownership.</returns>
    public ExternalQueueOperation BeginExternalQueueOperation()
    {
        throw new InvalidOperationException("The external texture view lease holds no external view.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.isDisposed = true;
    }
}
