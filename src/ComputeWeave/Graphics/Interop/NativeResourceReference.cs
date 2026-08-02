using System;
using ComputeWeave.Core.Extensions;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Win32;

namespace ComputeWeave.Interop;

/// <summary>
/// A reference keeping the resource generation backing a native object alive while that object is used outside the runtime.
/// </summary>
/// <remarks>
/// <para>
/// This type must always be used in a <see langword="using"/> statement and disposed properly. Not doing so is
/// undefined behavior and retains the resource generation for the lifetime of the device. Copying an instance
/// and disposing more than one of the copies is undefined behavior as well.
/// </para>
/// <para>
/// Holding a reference guarantees the lifetime of the generation, not the ordering of the work issued upon it.
/// The runtime does not track what the holder submits, and does not order it against its own submissions.
/// </para>
/// <para>
/// The holder must return the resource to the state it was acquired in. The runtime keeps recording the state
/// it observed at acquisition, and plans the barriers of its own submissions from it.
/// </para>
/// </remarks>
public unsafe struct NativeResourceReference : IDisposable
{
    /// <summary>
    /// The <see cref="GraphicsDevice"/> instance owning the referenced resource generation.
    /// </summary>
    private GraphicsDevice? device;

    /// <summary>
    /// The owner of the referenced resource generation, or <see langword="null"/> once released.
    /// </summary>
    private IResourceGenerationOwner? owner;

    /// <summary>
    /// The <see cref="ID3D12Resource"/> object the current reference holds, or <see langword="null"/> once released.
    /// </summary>
    private ID3D12Resource* d3D12Resource;

    /// <summary>
    /// The ordinal of the referenced resource within its owner.
    /// </summary>
    private readonly int resourceIndex;

    /// <summary>
    /// The identifier of the referenced resource generation.
    /// </summary>
    private readonly ResourceGenerationId generationId;

    /// <summary>
    /// The lease deferring the native release driven by the reference tracker of the referenced resource.
    /// </summary>
    /// <remarks>
    /// Borrowed resources release their native object from <c>DangerousOnDispose</c>, which the reference
    /// tracker drives and the generation reference count does not gate. The lease is what keeps the lifetime
    /// guarantee of a native reference true for them.
    /// </remarks>
    private ReferenceTracker.Lease lease;

    /// <summary>
    /// Creates a new <see cref="NativeResourceReference"/> instance with the specified parameters.
    /// </summary>
    /// <param name="device">The <see cref="GraphicsDevice"/> instance owning the referenced resource generation.</param>
    /// <param name="owner">The owner of the referenced resource generation.</param>
    /// <param name="resourceIndex">The ordinal of the referenced resource within <paramref name="owner"/>.</param>
    /// <param name="generationId">The identifier of the referenced resource generation.</param>
    /// <param name="d3D12Resource">The <see cref="ID3D12Resource"/> object the reference takes ownership of.</param>
    /// <param name="lease">The lease of the referenced resource the reference takes ownership of.</param>
    internal NativeResourceReference(
        GraphicsDevice device,
        IResourceGenerationOwner owner,
        int resourceIndex,
        ResourceGenerationId generationId,
        ID3D12Resource* d3D12Resource,
        ReferenceTracker.Lease lease)
    {
        this.device = device;
        this.owner = owner;
        this.d3D12Resource = d3D12Resource;
        this.resourceIndex = resourceIndex;
        this.generationId = generationId;
        this.lease = lease;
    }

    /// <summary>
    /// Gets whether the current reference still holds the resource generation it was acquired for.
    /// </summary>
    /// <remarks>
    /// This is <see langword="false"/> once the reference has been disposed, and also once a device or domain
    /// teardown has released the generation. Teardown does not wait for native references to be released.
    /// </remarks>
    public readonly bool IsValid
    {
        get
        {
            if (this.owner is not IResourceGenerationOwner owner)
            {
                return false;
            }

            return owner.GetResourceRecord(this.resourceIndex).Id == this.generationId;
        }
    }

    /// <summary>
    /// Gets the underlying COM object of the referenced resource, as a specified interface. This method invokes
    /// <see href="https://docs.microsoft.com/windows/win32/api/unknwn/nf-unknwn-iunknown-queryinterface(refiid_void)">IUnknown::QueryInterface</see>.
    /// </summary>
    /// <param name="riid">A reference to the interface identifier (IID) of the resource interface being queried for.</param>
    /// <param name="ppvObject">The address of a pointer to an interface with the IID specified in <paramref name="riid"/>.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the current reference is not valid.</exception>
    public readonly void QueryInterface(Guid* riid, void** ppvObject)
    {
        default(ObjectDisposedException).ThrowIfNull(this.owner);
        default(ObjectDisposedException).ThrowIf(!IsValid, this.owner);

        this.d3D12Resource->QueryInterface(riid, ppvObject).Assert();
    }

    /// <summary>
    /// Tries to get the underlying COM object of the referenced resource, as a specified interface.
    /// </summary>
    /// <param name="riid">A reference to the interface identifier (IID) of the resource interface being queried for.</param>
    /// <param name="ppvObject">The address of a pointer to an interface with the IID specified in <paramref name="riid"/>.</param>
    /// <returns>
    /// <c>S_OK</c> if the interface is supported, and <c>E_NOINTERFACE</c> otherwise.
    /// If the current reference is not valid, then this method returns <c>E_FAIL</c>.
    /// </returns>
    public readonly int TryQueryInterface(Guid* riid, void** ppvObject)
    {
        if (!IsValid)
        {
            return E.E_FAIL;
        }

        return this.d3D12Resource->QueryInterface(riid, ppvObject);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GraphicsDevice? device = this.device;
        IResourceGenerationOwner? owner = this.owner;
        ID3D12Resource* d3D12Resource = this.d3D12Resource;

        this.device = null;
        this.owner = null;
        this.d3D12Resource = null;

        if (owner is null)
        {
            return;
        }

        if (d3D12Resource is not null)
        {
            _ = d3D12Resource->Release();
        }

        device!.ReleaseNativeResource(owner, this.resourceIndex, this.generationId);

        this.lease.Dispose();
    }
}
