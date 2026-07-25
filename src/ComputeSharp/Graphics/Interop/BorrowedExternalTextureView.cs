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
    /// Gets whether the current borrow refers to a live external view.
    /// </summary>
    public bool IsValid => false;

    /// <summary>
    /// Gets the borrowed external view.
    /// </summary>
    /// <returns>The borrowed external view.</returns>
    public TView DangerousGetView()
    {
        throw new InvalidOperationException("The external texture view borrow is not valid.");
    }

    /// <summary>
    /// Releases the resources held by the current borrow.
    /// </summary>
    public void Dispose()
    {
    }
}
