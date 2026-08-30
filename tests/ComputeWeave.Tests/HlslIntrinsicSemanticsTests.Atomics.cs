using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <inheritdoc/>
partial class HlslIntrinsicSemanticsTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedAnd(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12]);

        device.Get().For(1, new InterlockedAndShader(buffer, 10));

        int[] result = buffer.ToArray();

        Assert.AreEqual(8, result[0]);
    }

    // 12 and 10 share only the bit worth eight. Every probe below guards on the thread index because a
    // dispatch runs a whole group, and repeating an operation is harmless for some of them but not for all
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedAndShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedAnd(ref this.buffer[0], this.value);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedOr(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12]);

        device.Get().For(1, new InterlockedOrShader(buffer, 10));

        int[] result = buffer.ToArray();

        Assert.AreEqual(14, result[0]);
    }

    // 12 or 10 sets the bits worth eight, four and two
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedOrShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedOr(ref this.buffer[0], this.value);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedXor(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12]);

        device.Get().For(1, new InterlockedXorShader(buffer, 10));

        int[] result = buffer.ToArray();

        Assert.AreEqual(6, result[0]);
    }

    // 12 exclusive or 10 keeps only the bits the two do not share. Applying it twice would undo it, which is
    // why the single thread guard matters here more than anywhere else
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedXorShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedXor(ref this.buffer[0], this.value);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedMin(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12]);

        device.Get().For(1, new InterlockedMinShader(buffer, 10));

        int[] result = buffer.ToArray();

        Assert.AreEqual(10, result[0]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedMinShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedMin(ref this.buffer[0], this.value);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedMax(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12]);

        device.Get().For(1, new InterlockedMaxShader(buffer, 10));

        int[] result = buffer.ToArray();

        Assert.AreEqual(12, result[0]);
    }

    // the seed and the operand differ, so min and max leave different values behind
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedMaxShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedMax(ref this.buffer[0], this.value);
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_InterlockedCompareStore(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>([12, 12]);

        device.Get().For(1, new InterlockedCompareStoreShader(buffer, 12, 7, 99));

        int[] result = buffer.ToArray();

        Assert.AreEqual(99, result[0]);
        Assert.AreEqual(12, result[1]);
    }

    // the middle argument is compared and the last one is stored. Reversing the two would leave the first
    // slot at 12, because the buffer holds 12 and not 99
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct InterlockedCompareStoreShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;
        public readonly int matching;
        public readonly int notMatching;
        public readonly int value;

        /// <inheritdoc/>
        public void Execute()
        {
            if (ThreadIds.X == 0)
            {
                Hlsl.InterlockedCompareStore(ref this.buffer[0], this.matching, this.value);
                Hlsl.InterlockedCompareStore(ref this.buffer[1], this.notMatching, this.value);
            }
        }
    }

    /// <summary>
    /// Asserts that every thread of a probe carrying a barrier still wrote its own index.
    /// </summary>
    /// <param name="results">The buffer the probe wrote.</param>
    private static void AssertEveryThreadWrote(ReadWriteBuffer<int> results)
    {
        int[] values = results.ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(i, values[i]);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllMemoryBarrier(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new AllMemoryBarrierShader(buffer));

        AssertEveryThreadWrote(buffer);
    }

    // The five barriers have no value of their own to compare, and nothing they order can be asserted here:
    // the generated entry point wraps every body in a dispatch range check, so a barrier always sits inside a
    // branch. A barrier that synchronizes the group needs every thread of the group to reach it, which a
    // branch on the thread index does not promise. See issue #69. What these probes establish is that each
    // name reaches an intrinsic the shader compiler accepts, and that a thread still computes correctly with
    // one in the way. A name that stopped mapping to a real intrinsic would fail to compile, which is what
    // the coverage here is worth
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllMemoryBarrierShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            int value = ThreadIds.X;

            Hlsl.AllMemoryBarrier();

            this.buffer[ThreadIds.X] = value;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllMemoryBarrierWithGroupSync(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new AllMemoryBarrierWithGroupSyncShader(buffer));

        AssertEveryThreadWrote(buffer);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllMemoryBarrierWithGroupSyncShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            int value = ThreadIds.X;

            Hlsl.AllMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = value;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DeviceMemoryBarrier(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new DeviceMemoryBarrierShader(buffer));

        AssertEveryThreadWrote(buffer);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DeviceMemoryBarrierShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            int value = ThreadIds.X;

            Hlsl.DeviceMemoryBarrier();

            this.buffer[ThreadIds.X] = value;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DeviceMemoryBarrierWithGroupSync(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new DeviceMemoryBarrierWithGroupSyncShader(buffer));

        AssertEveryThreadWrote(buffer);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DeviceMemoryBarrierWithGroupSyncShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            int value = ThreadIds.X;

            Hlsl.DeviceMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = value;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_GroupMemoryBarrierWithGroupSync(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new GroupMemoryBarrierWithGroupSyncShader(buffer));

        AssertEveryThreadWrote(buffer);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GroupMemoryBarrierWithGroupSyncShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            int value = ThreadIds.X;

            Hlsl.GroupMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = value;
        }
    }
}
