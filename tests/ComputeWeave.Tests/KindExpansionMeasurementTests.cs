using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

[TestClass]
public partial class KindExpansionMeasurementTests
{
    // Modf(double) measured separately and confirmed rejected by DXC (narrows to float, same as IsNaN/IsFinite/IsInfinite)

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_ModfUInt(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(2);

        device.Get().For(1, new ModfUIntShader(results, 7u));

        uint2[] pairs = results.ToArray();

        Assert.AreEqual(0u, pairs[0].X);
        Assert.AreEqual(0u, pairs[0].Y);
        Assert.AreEqual(7u, pairs[1].X);
        Assert.AreEqual(7u, pairs[1].Y);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ModfUIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint value;

        /// <inheritdoc/>
        public void Execute()
        {
            uint fractional = Hlsl.Modf(this.value, out uint integer);

            this.results[0] = new uint2(fractional, 0u);
            this.results[1] = new uint2(integer, 7u);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(2);

        device.Get().For(1, new AllDoubleShader(results, 2.5, 0.0));

        int[] values = results.ToArray();

        Assert.AreEqual(1, values[0]);
        Assert.AreEqual(0, values[1]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly double nonZero;
        public readonly double zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.All(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.All(this.zero) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AllUInt(Device device)
    {
        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(2);

        device.Get().For(1, new AllUIntShader(results, 5u, 0u));

        int[] values = results.ToArray();

        Assert.AreEqual(1, values[0]);
        Assert.AreEqual(0, values[1]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllUIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly uint nonZero;
        public readonly uint zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.All(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.All(this.zero) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AnyDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(2);

        device.Get().For(1, new AnyDoubleShader(results, 2.5, 0.0));

        int[] values = results.ToArray();

        Assert.AreEqual(1, values[0]);
        Assert.AreEqual(0, values[1]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AnyDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly double nonZero;
        public readonly double zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Any(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.Any(this.zero) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AnyUInt(Device device)
    {
        using ReadWriteBuffer<int> results = device.Get().AllocateReadWriteBuffer<int>(2);

        device.Get().For(1, new AnyUIntShader(results, 5u, 0u));

        int[] values = results.ToArray();

        Assert.AreEqual(1, values[0]);
        Assert.AreEqual(0, values[1]);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AnyUIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int> results;
        public readonly uint nonZero;
        public readonly uint zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.Any(this.nonZero) ? 1 : 0;
            this.results[1] = Hlsl.Any(this.zero) ? 1 : 0;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_MulDoubleScalar(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(1);

        device.Get().For(1, new MulDoubleScalarShader(results, 3.0, 4.0));

        double2[] pairs = results.ToArray();

        Assert.AreEqual(12.0, pairs[0].X, 0.0);
        Assert.AreEqual(12.0, pairs[0].Y, 0.0);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MulDoubleScalarShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double a;
        public readonly double b;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new double2(Hlsl.Mul(this.a, this.b), this.a * this.b);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_MulUIntScalar(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(1);

        device.Get().For(1, new MulUIntScalarShader(results, 3u, 4u));

        uint2[] pairs = results.ToArray();

        Assert.AreEqual(12u, pairs[0].X);
        Assert.AreEqual(12u, pairs[0].Y);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct MulUIntScalarShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint a;
        public readonly uint b;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.Mul(this.a, this.b), this.a * this.b);
        }
    }
}
