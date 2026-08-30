using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <inheritdoc/>
partial class VectorConstantSemanticsTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Double2Constants(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(4);

        device.Get().For(1, new Double2ConstantShader(results));

        double2[] values = results.ToArray();

        AssertDouble2(double2.Zero, values[0], "Zero");
        AssertDouble2(double2.One, values[1], "One");
        AssertDouble2(double2.UnitX, values[2], "UnitX");
        AssertDouble2(double2.UnitY, values[3], "UnitY");
    }

    // the buffer element carries doubles, so allocating it already needs the device to support them
    private static void AssertDouble2(double2 expected, double2 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Double2ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = double2.Zero;
            this.results[1] = double2.One;
            this.results[2] = double2.UnitX;
            this.results[3] = double2.UnitY;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Double3Constants(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double3> results = device.Get().AllocateReadWriteBuffer<double3>(5);

        device.Get().For(1, new Double3ConstantShader(results));

        double3[] values = results.ToArray();

        AssertDouble3(double3.Zero, values[0], "Zero");
        AssertDouble3(double3.One, values[1], "One");
        AssertDouble3(double3.UnitX, values[2], "UnitX");
        AssertDouble3(double3.UnitY, values[3], "UnitY");
        AssertDouble3(double3.UnitZ, values[4], "UnitZ");
    }

    private static void AssertDouble3(double3 expected, double3 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Double3ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double3> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = double3.Zero;
            this.results[1] = double3.One;
            this.results[2] = double3.UnitX;
            this.results[3] = double3.UnitY;
            this.results[4] = double3.UnitZ;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Double4Constants(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double4> results = device.Get().AllocateReadWriteBuffer<double4>(6);

        device.Get().For(1, new Double4ConstantShader(results));

        double4[] values = results.ToArray();

        AssertDouble4(double4.Zero, values[0], "Zero");
        AssertDouble4(double4.One, values[1], "One");
        AssertDouble4(double4.UnitX, values[2], "UnitX");
        AssertDouble4(double4.UnitY, values[3], "UnitY");
        AssertDouble4(double4.UnitZ, values[4], "UnitZ");
        AssertDouble4(double4.UnitW, values[5], "UnitW");
    }

    private static void AssertDouble4(double4 expected, double4 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
        Assert.AreEqual(expected.W, actual.W, $"{name}.W");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Double4ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double4> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = double4.Zero;
            this.results[1] = double4.One;
            this.results[2] = double4.UnitX;
            this.results[3] = double4.UnitY;
            this.results[4] = double4.UnitZ;
            this.results[5] = double4.UnitW;
        }
    }
}
