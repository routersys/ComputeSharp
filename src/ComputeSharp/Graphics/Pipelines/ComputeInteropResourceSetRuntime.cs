using System;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Interop;
using ComputeSharp.Resources.Lifetime;

namespace ComputeSharp;

/// <summary>
/// The runtime of a generated compute interop resource set, owning its registration and its shared slot generations.
/// </summary>
public sealed class ComputeInteropResourceSetRuntime : IDisposable
{
    /// <summary>
    /// The registry the current resource set is registered on.
    /// </summary>
    private readonly DeviceRegistrationRegistry registry;

    /// <summary>
    /// The registration of the current resource set.
    /// </summary>
    private readonly InteropResourceSetRuntime runtime;

    /// <summary>
    /// Creates a new <see cref="ComputeInteropResourceSetRuntime"/> instance with the specified parameters.
    /// </summary>
    /// <param name="registry">The registry the current resource set is registered on.</param>
    /// <param name="runtime">The registration of the current resource set.</param>
    private ComputeInteropResourceSetRuntime(DeviceRegistrationRegistry registry, InteropResourceSetRuntime runtime)
    {
        this.registry = registry;
        this.runtime = runtime;
    }

    /// <summary>
    /// Gets the device the current resource set is registered on.
    /// </summary>
    public GraphicsDevice Device => this.runtime.Device;

    /// <summary>
    /// Gets the interop domain the current resource set is registered against.
    /// </summary>
    public ComputeInteropDomain Domain => this.runtime.Domain;

    /// <summary>
    /// Gets whether disposal of the current resource set has been requested.
    /// </summary>
    public bool IsDisposeRequested => this.runtime.State is not (RegistrationState.Constructing or RegistrationState.Active);

    /// <summary>
    /// Registers a generated compute interop resource set against an interop domain.
    /// </summary>
    /// <param name="device">The device to register the resource set on.</param>
    /// <param name="domain">The interop domain the resource set shares its textures with.</param>
    /// <param name="canonicalDescriptor">The canonical binary descriptor of the resource set.</param>
    /// <param name="sharedSlots">The shared slots declared by the resource set, in slot ordinal order.</param>
    /// <returns>The <see cref="ComputeInteropResourceSetRuntime"/> instance of the registered resource set.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> or <paramref name="domain"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if the descriptor, the domain or the shared slots are not valid.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the device or the domain cannot accept the registration.</exception>
    public static ComputeInteropResourceSetRuntime Create(
        GraphicsDevice device,
        ComputeInteropDomain domain,
        ReadOnlySpan<byte> canonicalDescriptor,
        ReadOnlySpan<IComputeSharedResourceSlot> sharedSlots)
    {
        default(ArgumentNullException).ThrowIfNull(device);
        default(ArgumentNullException).ThrowIfNull(domain);

        using ReferenceTracker.Lease _0 = device.GetReferenceTracker().GetLease();

        device.ThrowIfDeviceTerminal();

        IComputeSharedSlot[] slots = new IComputeSharedSlot[sharedSlots.Length];

        for (int i = 0; i < sharedSlots.Length; i++)
        {
            default(ArgumentException).ThrowIf(sharedSlots[i] is not IComputeSharedSlot, nameof(sharedSlots));

            slots[i] = (IComputeSharedSlot)sharedSlots[i];
        }

        DeviceRegistrationRegistry registry = device.GetRegistrationRegistry();

        return new ComputeInteropResourceSetRuntime(registry, registry.RegisterResourceSet(domain, canonicalDescriptor, slots));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.runtime.RequestDispose();

        _ = this.registry.TryUnregisterResourceSet(this.runtime);
    }

    /// <summary>
    /// Waits for the disposal of the current resource set to complete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if disposal of the current resource set has not been requested.</exception>
    /// <remarks>
    /// The structural capacity of a resource set is returned once every shared slot has released its generations
    /// and its maintenance has completed, so this blocks until the completion coordinator gets there.
    /// </remarks>
    public void WaitForDisposal()
    {
        default(InvalidOperationException).ThrowIf(
            !IsDisposeRequested,
            "The compute interop resource set has not been disposed.");

        CompletionCoordinator coordinator = this.registry.Coordinator;

        while (true)
        {
            ulong progress = coordinator.ProgressVersion;

            this.runtime.RunSharedSlotMaintenance();

            if (this.runtime.TryCompleteDeferredRelease())
            {
                return;
            }

            default(InvalidOperationException).ThrowIf(
                !coordinator.TryWaitForProgress(progress),
                "The completion coordinator of the device stopped before the resource set was released.");
        }
    }
}
