using ComputeWeave.Descriptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class ThreadGroupSizeTests
{
    [TestMethod]
    public unsafe void Verify_ThreadGroupSize()
    {
        static (int X, int Y, int Z) GetNumThreads<T>()
            where T : struct, IComputeShaderDescriptor<T>
        {
            return (T.ThreadsX, T.ThreadsY, T.ThreadsZ);
        }

        Assert.AreEqual((64, 1, 1), GetNumThreads<DispatchXShader>());
        Assert.AreEqual((1, 64, 1), GetNumThreads<DispatchYShader>());
        Assert.AreEqual((1, 1, 64), GetNumThreads<DispatchZShader>());
        Assert.AreEqual((8, 8, 1), GetNumThreads<DispatchXYShader>());
        Assert.AreEqual((8, 1, 8), GetNumThreads<DispatchXZShader>());
        Assert.AreEqual((1, 8, 8), GetNumThreads<DispatchYZShader>());
        Assert.AreEqual((4, 4, 4), GetNumThreads<DispatchXYZShader>());
        Assert.AreEqual((11, 14, 6), GetNumThreads<DispatchCustomShader>());
    }

    [TestMethod]
    public void Verify_ThreadGroupSizeAttributeMatchesDescriptor()
    {
        // The attribute computes the thread group size for a default axis on its own, separately from
        // the generator. Both mappings have to agree, or the two ways to read the size disagree.
        static void VerifyDefault<T>(DefaultThreadGroupSizes size)
            where T : struct, IComputeShaderDescriptor<T>
        {
            ThreadGroupSizeAttribute attribute = new(size);

            Assert.AreEqual((T.ThreadsX, T.ThreadsY, T.ThreadsZ), (attribute.ThreadsX, attribute.ThreadsY, attribute.ThreadsZ), $"{size}");
        }

        VerifyDefault<DispatchXShader>(DefaultThreadGroupSizes.X);
        VerifyDefault<DispatchYShader>(DefaultThreadGroupSizes.Y);
        VerifyDefault<DispatchZShader>(DefaultThreadGroupSizes.Z);
        VerifyDefault<DispatchXYShader>(DefaultThreadGroupSizes.XY);
        VerifyDefault<DispatchXZShader>(DefaultThreadGroupSizes.XZ);
        VerifyDefault<DispatchYZShader>(DefaultThreadGroupSizes.YZ);
        VerifyDefault<DispatchXYZShader>(DefaultThreadGroupSizes.XYZ);

        // The explicit constructor has to carry the values through unchanged as well
        static void VerifyExplicit<T>(int threadsX, int threadsY, int threadsZ)
            where T : struct, IComputeShaderDescriptor<T>
        {
            ThreadGroupSizeAttribute attribute = new(threadsX, threadsY, threadsZ);

            Assert.AreEqual((T.ThreadsX, T.ThreadsY, T.ThreadsZ), (attribute.ThreadsX, attribute.ThreadsY, attribute.ThreadsZ));
        }

        VerifyExplicit<DispatchCustomShader>(11, 14, 6);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchXShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.Y)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchYShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.Z)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchZShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchXYShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchXZShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.YZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchYZShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.XYZ)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchXYZShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }

    [AutoConstructor]
    [ThreadGroupSize(11, 14, 6)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DispatchCustomShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> buffer;

        public void Execute()
        {
            this.buffer[ThreadIds.X] = 0;
        }
    }
}
