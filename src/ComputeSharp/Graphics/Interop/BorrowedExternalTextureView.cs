using System;

namespace ComputeSharp;

/// <summary>
/// A transient borrow of the external view of a shared texture generation.
/// </summary>
/// <typeparam name="TView">The type of the external view.</typeparam>
public readonly ref struct BorrowedExternalTextureView<TView>
    where TView : class
{
    /// <summary>
    /// The borrowed external view, or <see langword="null"/> for an invalid borrow.
    /// </summary>
    private readonly TView? view;

    /// <summary>
    /// Creates a new <see cref="BorrowedExternalTextureView{TView}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="view">The borrowed external view.</param>
    internal BorrowedExternalTextureView(TView view)
    {
        this.view = view;
    }

    /// <summary>
    /// Gets whether the current borrow refers to a live external view.
    /// </summary>
    public bool IsValid => this.view is not null;

    /// <summary>
    /// Gets the borrowed external view.
    /// </summary>
    /// <returns>The borrowed external view.</returns>
    public TView DangerousGetView()
    {
        default(InvalidOperationException).ThrowIf(this.view is null, "The external texture view borrow is no longer valid.");

        return this.view!;
    }

    /// <summary>
    /// Releases the resources held by the current borrow.
    /// </summary>
    public void Dispose()
    {
    }
}
