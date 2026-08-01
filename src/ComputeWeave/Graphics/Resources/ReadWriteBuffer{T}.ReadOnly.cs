using System;
using System.Runtime.CompilerServices;
using ComputeWeave.Interop;
using ComputeWeave.Resources.Interop;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Win32;

namespace ComputeWeave;

/// <inheritdoc/>
partial class ReadWriteBuffer<T>
{
    /// <summary>
    /// The wrapping <see cref="ReadOnly"/> instance, if available.
    /// </summary>
    private ReadOnly? readOnlyWrapper;

    /// <inheritdoc cref="ReadWriteBufferExtensions.AsReadOnly{T}(ReadWriteBuffer{T})"/>
    internal IReadOnlyBuffer<T> AsReadOnly()
    {
        using ReferenceTracker.Lease _0 = GraphicsDevice.GetReferenceTracker().GetLease();
        using ReferenceTracker.Lease _1 = GetReferenceTracker().GetLease();

        GraphicsDevice.ThrowIfDeviceLost();

        ReadOnly? readOnlyWrapper = this.readOnlyWrapper;

        if (readOnlyWrapper is not null)
        {
            return readOnlyWrapper;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ReadOnly InitializeWrapper(ReadWriteBuffer<T> buffer)
        {
            return buffer.readOnlyWrapper = new(buffer);
        }

        return InitializeWrapper(this);
    }

    /// <summary>
    /// A wrapper for a <see cref="ReadWriteBuffer{T}"/> resource that binds it through its SRV.
    /// </summary>
    /// <param name="owner">The owning <see cref="ReadWriteBuffer{T}"/> instance to wrap.</param>
    /// <remarks>
    /// The wrapper holds no unmanaged resource of its own. The SRV it binds and the native resource it refers
    /// to are both owned by <paramref name="owner"/>, so its lifetime is exactly the lifetime of the owner and
    /// every lease it hands out is taken on the owner.
    /// </remarks>
    private sealed unsafe class ReadOnly(ReadWriteBuffer<T> owner) : IReadOnlyBuffer<T>, ID3D12ReadOnlyResource, IGenerationBoundResource
    {
        /// <summary>
        /// The owning <see cref="ReadWriteBuffer{T}"/> instance being wrapped.
        /// </summary>
        private readonly ReadWriteBuffer<T> owner = owner;

        /// <inheritdoc/>
        public ref readonly T this[int i] => throw new InvalidExecutionContextException($"{typeof(ReadWriteBuffer<T>.ReadOnly)}[{typeof(int)}]");

        /// <inheritdoc/>
        public int Length => this.owner.Length;

        /// <inheritdoc/>
        public GraphicsDevice GraphicsDevice => this.owner.GraphicsDevice;

        /// <inheritdoc/>
        D3D12_GPU_DESCRIPTOR_HANDLE ID3D12ReadOnlyResource.ValidateAndGetGpuDescriptorHandle(GraphicsDevice device)
        {
            using ReferenceTracker.Lease _0 = this.owner.GetReferenceTracker().GetLease();

            this.owner.ThrowIfDeviceMismatch(device);

            return this.owner.D3D12ShaderResourceViewGpuDescriptorHandle;
        }

        /// <inheritdoc/>
        ID3D12Resource* ID3D12ReadOnlyResource.ValidateAndGetID3D12Resource(GraphicsDevice device, out ReferenceTracker.Lease lease)
        {
            lease = this.owner.GetReferenceTracker().GetLease();

            this.owner.ThrowIfDeviceMismatch(device);

            return this.owner.D3D12Resource;
        }

        /// <inheritdoc/>
        void IGenerationBoundResource.BindGeneration(IResourceGenerationOwner owner, int resourceIndex)
        {
            default(NotSupportedException).Throw();
        }

        /// <inheritdoc/>
        bool IGenerationBoundResource.TryGetGenerationBinding(out ResourceUsageBinding binding)
        {
            if (!((IGenerationBoundResource)this.owner).TryGetGenerationBinding(out binding))
            {
                return false;
            }

            binding = binding.AsReadOnlyAccess();

            return true;
        }
    }
}
