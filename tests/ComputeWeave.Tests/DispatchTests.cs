using System;
using ComputeWeave.Interop;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

[TestClass]
public partial class DispatchTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public unsafe void Verify_ThreadIds(Device device)
    {
        using ReadWriteTexture3D<int4> buffer = device.Get().AllocateReadWriteTexture3D<int4>(50, 50, 50);

        device.Get().For(buffer.Width, buffer.Height, buffer.Depth, new ThreadIdsShader(buffer));

        int4[,,] data = buffer.ToArray();
        int* value = stackalloc int[4];

        for (int z = 0; z < 50; z++)
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    *(int4*)value = data[z, y, x];

                    Assert.AreEqual(x, value[0]);
                    Assert.AreEqual(y, value[1]);
                    Assert.AreEqual(z, value[2]);
                }
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(0, 8, 8)]
    [Data(8, 0, 8)]
    [Data(8, 8, 0)]
    [Data(8, -3, 16)]
    [Data(-1, -1, -1)]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Verify_ThreadIds_OutOfRange(Device device, int x, int y, int z)
    {
        using ReadWriteTexture3D<int4> buffer = device.Get().AllocateReadWriteTexture3D<int4>(50, 50, 50);

        device.Get().For(x, y, z, new ThreadIdsShader(buffer));

        Assert.Fail();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(int.MaxValue, 1, 1, "groupsX")]
    [Data(1, int.MaxValue, 1, "groupsY")]
    [Data(1, 1, int.MaxValue, "groupsZ")]
    public void Verify_ThreadIds_OutOfRange_ParameterName(Device device, int x, int y, int z, string parameterName)
    {
        using ReadWriteTexture3D<int4> buffer = device.Get().AllocateReadWriteTexture3D<int4>(50, 50, 50);

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => device.Get().For(x, y, z, new ThreadIdsShader(buffer)));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XYZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ThreadIdsShader : IComputeShader
    {
        public readonly ReadWriteTexture3D<int4> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.XYZ].XYZ = ThreadIds.XYZ;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    [Data(1, 1, 1)]
    [Data(1, 1, 2)]
    [Data(1, 2, 1)]
    [Data(2, 1, 1)]
    [Data(10, 1, 1)]
    [Data(10, 1, 20)]
    [Data(1, 2, 3)]
    [Data(2, 3, 4)]
    [Data(3, 2, 1)]
    [Data(10, 20, 30)]
    [Data(10, 2, 3)]
    public unsafe void Verify_ThreadIdsNormalized(Device device, int width, int height, int depth)
    {
        using ReadWriteTexture3D<float4> buffer = device.Get().AllocateReadWriteTexture3D<float4>(width, height, depth);

        device.Get().For(buffer.Width, buffer.Height, buffer.Depth, new ThreadIdsNormalizedShader(buffer));

        float4[,,] data = buffer.ToArray();
        float* value = stackalloc float[4];

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    *(float4*)value = data[z, y, x];

                    float expectedX = width == 1 ? 0 : (x / (float)(buffer.Width - 1));
                    float expectedY = height == 1 ? 0 : (y / (float)(buffer.Height - 1));
                    float expectedZ = depth == 1 ? 0 : (z / (float)(buffer.Depth - 1));

                    Assert.AreEqual(expectedX, value[0], 0.000001f);
                    Assert.AreEqual(expectedY, value[1], 0.000001f);
                    Assert.AreEqual(expectedZ, value[2], 0.000001f);
                    Assert.AreEqual(expectedX, value[3], 0.000001f);
                }
            }
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XYZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ThreadIdsNormalizedShader : IComputeShader
    {
        public readonly ReadWriteTexture3D<float4> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.XYZ].XYZ = ThreadIds.Normalized.XYZ;
            this.buffer[ThreadIds.XYZ].XY = ThreadIds.Normalized.XY;
            this.buffer[ThreadIds.XYZ].W = ThreadIds.Normalized.X;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public unsafe void Verify_GroupIds(Device device)
    {
        using ReadWriteTexture3D<int4> buffer = device.Get().AllocateReadWriteTexture3D<int4>(50, 50, 50);

        device.Get().For(buffer.Width, buffer.Height, buffer.Depth, new GroupIdsShader(buffer));

        int4[,,] data = buffer.ToArray();
        int* value = stackalloc int[4];

        for (int z = 0; z < 50; z++)
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    *(int4*)value = data[z, y, x];

                    Assert.AreEqual(x % 4, value[0]);
                    Assert.AreEqual(y % 4, value[1]);
                    Assert.AreEqual(z % 4, value[2]);
                    Assert.AreEqual((value[2] * 4 * 4) + (value[1] * 4) + value[0], value[3]);
                }
            }
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XYZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GroupIdsShader : IComputeShader
    {
        public readonly ReadWriteTexture3D<int4> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.XYZ].XYZ = GroupIds.XYZ;
            this.buffer[ThreadIds.XYZ].W = GroupIds.Index;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_GroupSize(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(32);

        device.Get().For(1, 1, 1, new GroupSizeShader(buffer));

        int[] data = buffer.ToArray();

        Assert.AreEqual(4, data[0]);
        Assert.AreEqual(15, data[1]);
        Assert.AreEqual(7, data[2]);
        Assert.AreEqual(4 * 15 * 7, data[3]);
        Assert.AreEqual(4 + 15, data[4]);
        Assert.AreEqual(4 + 15 + 7, data[5]);
    }

    [AutoConstructor]
    [ThreadGroupSize(4, 15, 7)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GroupSizeShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[0] = GroupSize.X;
            this.buffer[1] = GroupSize.Y;
            this.buffer[2] = GroupSize.Z;
            this.buffer[3] = GroupSize.Count;
            this.buffer[4] = (int)Hlsl.Dot(GroupSize.XY, float2.One);
            this.buffer[5] = (int)Hlsl.Dot(GroupSize.XYZ, float3.One);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_GridIds(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(256);

        device.Get().For(256, 1, 1, new GridIdsShader(buffer));

        int[] data = buffer.ToArray();

        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(data[i], i / 32);
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(32, 1, 1)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GridIdsShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = GridIds.X;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DispatchSize(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(6);

        device.Get().For(11, 22, 3, new DispatchSizeShader(buffer));

        ShaderInfo info = ReflectionServices.GetShaderInfo<DispatchSizeShader>();

        int[] data = buffer.ToArray();

        Assert.AreEqual(data[0], 11 * 22 * 3);
        Assert.AreEqual(data[1], 11);
        Assert.AreEqual(data[2], 22);
        Assert.AreEqual(data[3], 3);
        Assert.AreEqual(data[4], 11 + 22);
        Assert.AreEqual(data[5], 11 + 22 + 3);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XYZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchSizeShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[0] = DispatchSize.Count;
            this.buffer[1] = DispatchSize.X;
            this.buffer[2] = DispatchSize.Y;
            this.buffer[3] = DispatchSize.Z;
            this.buffer[4] = (int)Hlsl.Dot(DispatchSize.XY, float2.One);
            this.buffer[5] = (int)Hlsl.Dot(DispatchSize.XYZ, float3.One);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DispatchAsPixelShader(Device device)
    {
        using ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(256, 256);

        device.Get().ForEach<DispatchPixelShader, float4>(texture);

        Rgba32[,] data = texture.ToArray();

        for (int y = 0; y < texture.Height; y++)
        {
            for (int x = 0; x < texture.Width; x++)
            {
                Rgba32 pixel = data[y, x];

                Assert.AreEqual((float)pixel.R / 255, (float)x / texture.Width, 0.1f);
                Assert.AreEqual((float)pixel.G / 255, (float)y / texture.Height, 0.1f);
                Assert.AreEqual(pixel.B, 255);
                Assert.AreEqual(pixel.A, 255);
            }
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchPixelShader : IComputeShader<float4>
    {
        public float4 Execute()
        {
            return new(
                (float)ThreadIds.X / DispatchSize.X,
                (float)ThreadIds.Y / DispatchSize.Y,
                1,
                1);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_GroupShared_WithFixedSize(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(256, new FixedGroupSharedPixelShader(buffer));

        int[] result = buffer.ToArray();

        for (int i = 0; i < 128; i++)
        {
            Assert.AreEqual(result[i], i);
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FixedGroupSharedPixelShader : IComputeShader
    {
        private readonly ReadWriteBuffer<int> buffer;

        [GroupShared(128)]
        private static readonly int[] cache;

        public void Execute()
        {
            int index = ThreadIds.X / 2;
            bool isWritingToGroupShared = ThreadIds.X % 2 == 0;

            if (isWritingToGroupShared)
            {
                cache[index] = index;
            }

            Hlsl.GroupMemoryBarrier();

            if (!isWritingToGroupShared)
            {
                this.buffer[index] = cache[index];
            }
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_GroupShared_WithDynamicSize(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(32);

        device.Get().For(64, 1, 1, new DynamicGroupSharedPixelShader(buffer));

        int[] result = buffer.ToArray();

        for (int i = 0; i < 32; i++)
        {
            Assert.AreEqual(result[i], i);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Dispatch_WithManyResources(Device device)
    {
        using ReadWriteBuffer<int> buffer0 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer1 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer2 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer3 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer4 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer5 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer6 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer7 = device.Get().AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> buffer8 = device.Get().AllocateReadWriteBuffer<int>(64);

        device.Get().For(64, new ManyResourcesShader(buffer0, buffer1, buffer2, buffer3, buffer4, buffer5, buffer6, buffer7, buffer8));

        ReadWriteBuffer<int>[] buffers = [buffer0, buffer1, buffer2, buffer3, buffer4, buffer5, buffer6, buffer7, buffer8];

        // Each buffer gets a distinct offset, so a resource bound to the wrong slot changes the values
        for (int i = 0; i < buffers.Length; i++)
        {
            int[] result = buffers[i].ToArray();

            for (int j = 0; j < result.Length; j++)
            {
                Assert.AreEqual(j + (i * 100), result[j]);
            }
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ManyResourcesShader : IComputeShader
    {
        private readonly ReadWriteBuffer<int> buffer0;
        private readonly ReadWriteBuffer<int> buffer1;
        private readonly ReadWriteBuffer<int> buffer2;
        private readonly ReadWriteBuffer<int> buffer3;
        private readonly ReadWriteBuffer<int> buffer4;
        private readonly ReadWriteBuffer<int> buffer5;
        private readonly ReadWriteBuffer<int> buffer6;
        private readonly ReadWriteBuffer<int> buffer7;
        private readonly ReadWriteBuffer<int> buffer8;

        public void Execute()
        {
            this.buffer0[ThreadIds.X] = ThreadIds.X;
            this.buffer1[ThreadIds.X] = ThreadIds.X + 100;
            this.buffer2[ThreadIds.X] = ThreadIds.X + 200;
            this.buffer3[ThreadIds.X] = ThreadIds.X + 300;
            this.buffer4[ThreadIds.X] = ThreadIds.X + 400;
            this.buffer5[ThreadIds.X] = ThreadIds.X + 500;
            this.buffer6[ThreadIds.X] = ThreadIds.X + 600;
            this.buffer7[ThreadIds.X] = ThreadIds.X + 700;
            this.buffer8[ThreadIds.X] = ThreadIds.X + 800;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(32, 1, 1)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DynamicGroupSharedPixelShader : IComputeShader
    {
        private readonly ReadWriteBuffer<int> buffer;

        [GroupShared]
        private static readonly int[] cache;

        public void Execute()
        {
            int index = ThreadIds.X / 2;
            bool isWritingToGroupShared = ThreadIds.X % 2 == 0;

            if (isWritingToGroupShared)
            {
                cache[index] = index;
            }

            Hlsl.GroupMemoryBarrier();

            if (!isWritingToGroupShared)
            {
                this.buffer[index] = cache[index];
            }
        }
    }

    /// <summary>
    /// A shader that waits for its whole thread group, run over a range that holds whole groups.
    /// </summary>
    /// <remarks>
    /// Every thread writes its own slot of the group shared array, waits, and reads the slot of the thread
    /// at the other end of the group. The read only has an answer if every thread of the group ran, which
    /// is what the range being a multiple of the thread group size gives.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FullThreadGroups_WholeGroupsAreHandedOver(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        device.Get().For(128, new GroupHandoffShader(buffer));

        int[] result = buffer.ToArray();

        for (int i = 0; i < 128; i++)
        {
            Assert.AreEqual((i / 64 * 64) + 63 - (i % 64), result[i]);
        }
    }

    /// <summary>
    /// The same shader over a range that leaves the last thread group partly outside it.
    /// </summary>
    /// <remarks>
    /// Before the range was rejected, this produced wrong values rather than an error: the threads outside
    /// the range never wrote their slot, so the ones inside it read a slot nothing had written. Measured on
    /// 2026-08-31 over a range of 100, 28 of the 100 values disagreed, on both devices and on every run.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    [Data(100, "x")]
    [Data(65, "x")]
    public void Verify_FullThreadGroups_PartialGroupIsRejected(Device device, int x, string parameterName)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(x);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => device.Get().For(x, new GroupHandoffShader(buffer)));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    /// <summary>
    /// The axis the range is rejected for is the one that is not a multiple.
    /// </summary>
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FullThreadGroups_TheReportedAxisIsTheOneAtFault(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(128);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => device.Get().For(64, 3, new PlanarGroupHandoffShader(buffer)));

        Assert.AreEqual("y", exception.ParamName);
    }

    /// <summary>
    /// The Z axis of the range, which the two rows above leave alone.
    /// </summary>
    /// <remarks>
    /// The shader the rows above use has a thread group one thread deep, so its Z axis has no remainder to
    /// find whatever range it is given. This one is four deep, and the range is not a multiple of that.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FullThreadGroups_TheDepthAxisIsChecked(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(64);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => device.Get().For(4, 4, 6, new VolumeGroupHandoffShader(buffer)));

        Assert.AreEqual("z", exception.ParamName);
    }

    /// <summary>
    /// A pixel shader like type, whose range comes from the texture rather than from an argument.
    /// </summary>
    /// <remarks>
    /// The requirement reaches this path through a second check, which the rows above do not run. One row
    /// per side, each leaving the other side a multiple, so a check that lost one of the two sides keeps
    /// the row for the side it still reads and fails the other.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    [Data(100, 64)]
    [Data(64, 100)]
    public void Verify_FullThreadGroups_ATextureThatIsNoMultipleIsRejected(Device device, int width, int height)
    {
        using ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(width, height);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => device.Get().ForEach<GroupSyncPixelShader, float4>(texture));

        Assert.AreEqual("texture", exception.ParamName);
    }

    /// <summary>
    /// The control for the row above. A texture whose sides are multiples is handed over as it is.
    /// </summary>
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FullThreadGroups_ATextureThatIsAMultipleIsAccepted(Device device)
    {
        using ReadWriteTexture2D<Rgba32, float4> texture = device.Get().AllocateReadWriteTexture2D<Rgba32, float4>(64, 64);

        device.Get().ForEach<GroupSyncPixelShader, float4>(texture);
    }

    /// <summary>
    /// The control. A shader that does not wait for its group keeps taking a range of any size.
    /// </summary>
    /// <remarks>
    /// Without this row, rejecting every range that is not a multiple would pass the rows above just as well,
    /// and the requirement would reach shaders that have no need of it.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AShaderThatDoesNotWaitTakesAnyRange(Device device)
    {
        using ReadWriteBuffer<int> buffer = device.Get().AllocateReadWriteBuffer<int>(100);

        device.Get().For(100, new PerThreadShader(buffer));

        int[] result = buffer.ToArray();

        for (int i = 0; i < 100; i++)
        {
            Assert.AreEqual(i, result[i]);
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GroupHandoffShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        [GroupShared(64)]
        private static readonly int[] cache;

        /// <inheritdoc/>
        public void Execute()
        {
            cache[GroupIds.X] = ThreadIds.X;

            Hlsl.GroupMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = cache[63 - GroupIds.X];
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(64, 2, 1)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct PlanarGroupHandoffShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        [GroupShared(128)]
        private static readonly int[] cache;

        /// <inheritdoc/>
        public void Execute()
        {
            cache[GroupIds.X] = ThreadIds.X;

            Hlsl.GroupMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = cache[63 - GroupIds.X];
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(4, 4, 4)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct VolumeGroupHandoffShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        [GroupShared(64)]
        private static readonly int[] cache;

        /// <inheritdoc/>
        public void Execute()
        {
            cache[GroupIds.X] = ThreadIds.X;

            Hlsl.GroupMemoryBarrierWithGroupSync();

            this.buffer[ThreadIds.X] = cache[GroupIds.X];
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct GroupSyncPixelShader : IComputeShader<float4>
    {
        /// <inheritdoc/>
        public float4 Execute()
        {
            // The barrier alone declares the requirement, which is the whole of what this shader is for
            Hlsl.GroupMemoryBarrierWithGroupSync();

            return new float4(ThreadIds.X, ThreadIds.Y, 0, 1);
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct PerThreadShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        /// <inheritdoc/>
        public void Execute()
        {
            this.buffer[ThreadIds.X] = ThreadIds.X;
        }
    }
}