using System;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// A transient borrow of the external view of a shared texture generation.
/// </summary>
/// <typeparam name="TView">The type of the external view.</typeparam>
public readonly ref struct BorrowedExternalTextureView<TView>
    where TView : class
{
    /// <summary>
    /// The external queue ownership the current borrow holds.
    /// </summary>
    private readonly ExternalQueueOperation operation;

    /// <summary>
    /// The generation the current borrow holds an external reference of.
    /// </summary>
    private readonly ResourceGenerationPin pin;

    /// <summary>
    /// The borrowed external view.
    /// </summary>
    private readonly TView? view;

    /// <summary>
    /// Creates a new <see cref="BorrowedExternalTextureView{TView}"/> instance with the specified parameters.
    /// </summary>
    /// <param name="operation">The external queue ownership the borrow holds.</param>
    /// <param name="pin">The generation the borrow holds an external reference of.</param>
    /// <param name="view">The borrowed external view.</param>
    internal BorrowedExternalTextureView(scoped in ExternalQueueOperation operation, scoped in ResourceGenerationPin pin, TView view)
    {
        this.operation = operation;
        this.pin = pin;
        this.view = view;
    }

    /// <summary>
    /// Gets whether the current borrow refers to a live external view.
    /// </summary>
    public bool IsValid => this.operation.IsValid;

    /// <summary>
    /// Gets the borrowed external view.
    /// </summary>
    /// <returns>The borrowed external view.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the current borrow is not valid.</exception>
    /// <exception cref="Exception">Rethrown from the failure the domain of the borrow or its device was left with.</exception>
    public TView DangerousGetView()
    {
        if (this.operation.Domain is not ComputeInteropDomain domain || this.view is not TView borrowedView || !IsValid)
        {
            throw new InvalidOperationException("The external texture view borrow is not valid.");
        }

        domain.ThrowIfPoisonedOrDeviceTerminal();

        return borrowedView;
    }

    /// <summary>
    /// Gets whether the borrowed generation is available to the external queue.
    /// </summary>
    /// <returns>Whether the borrowed generation is available to the external queue.</returns>
    internal bool IsBoundGenerationAvailable()
    {
        return this.operation.IsBoundGenerationAvailable(in this.pin);
    }

    /// <summary>
    /// Releases the resources held by the current borrow.
    /// </summary>
    public void Dispose()
    {
        this.operation.ReleaseBorrow(in this.pin);
    }
}
