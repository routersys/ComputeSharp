namespace ComputeSharp;

/// <summary>
/// An external authority that hands out memory grants to graphics devices.
/// </summary>
public interface IGraphicsMemoryBudgetBroker
{
    /// <summary>
    /// Registers a new client for a given graphics device.
    /// </summary>
    /// <param name="descriptor">The description of the device being registered.</param>
    /// <returns>A client that is reference distinct from every other client the broker has returned.</returns>
    /// <remarks>Implementations must be thread safe and must not call back into the runtime.</remarks>
    IGraphicsMemoryBudgetClient RegisterClient(in GraphicsMemoryClientDescriptor descriptor);
}
