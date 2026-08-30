using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <inheritdoc/>
partial class HlslIntrinsicSemanticsTests
{
    /// <summary>
    /// Asserts that every slot a probe wrote holds two agreeing values.
    /// </summary>
    /// <param name="results">The buffer the probe wrote.</param>
    /// <param name="tolerance">The allowed absolute difference between the two sides.</param>
    private static void AssertAgrees(ReadWriteBuffer<double2> results, double tolerance)
    {
        double2[] pairs = results.ToArray();

        for (int i = 0; i < pairs.Length; i++)
        {
            Assert.AreEqual(pairs[i].Y, pairs[i].X, tolerance, $"slot {i}");
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AsDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(1);

        device.Get().For(1, new AsDoubleShader(results, 0x00000000u, 0x3FF00000u, 1.0));

        AssertAgrees(results, 0.0);
    }

    // 1.0 is 0x3FF0000000000000, so the high half carries all the bits and the low half is zero.
    // Reversing the two arguments yields a subnormal instead, which is nowhere near 1
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly uint low;
        public readonly uint high;
        public readonly double expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new double2(Hlsl.AsDouble(this.low, this.high), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_BoolToDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(2);

        device.Get().For(1, new BoolToDoubleShader(results, 1.0f, -1.0f));

        AssertAgrees(results, 0.0);
    }

    // the conversion turns a true into one and a false into zero
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct BoolToDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly float positive;
        public readonly float negative;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new double2(Hlsl.BoolToDouble(this.positive > 0.0f), 1.0);
            this.results[1] = new double2(Hlsl.BoolToDouble(this.negative > 0.0f), 0.0);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DoubleToBool(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(2);

        device.Get().For(1, new DoubleToBoolShader(results, 2.5, 0.0));

        AssertAgrees(results, 0.0);
    }

    // every value other than zero converts to true
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DoubleToBoolShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double nonZero;
        public readonly double zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new double2(Hlsl.DoubleToBool(this.nonZero) ? 1.0 : 0.0, 1.0);
            this.results[1] = new double2(Hlsl.DoubleToBool(this.zero) ? 1.0 : 0.0, 0.0);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FusedMultiplyAdd(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(1);

        device.Get().For(1, new FusedMultiplyAddShader(results, 3.0, 5.0, 7.0));

        AssertAgrees(results, 0.0);
    }

    // the first two arguments multiply and the third adds, so 3, 5 and 7 give 22 rather than the 26 a
    // rotation of the arguments would give. The operands are small integers, so the fused form is exact
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FusedMultiplyAddShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double a;
        public readonly double b;
        public readonly double c;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new double2(Hlsl.FusedMultiplyAdd(this.a, this.b, this.c), (this.a * this.b) + this.c);
        }
    }
}
