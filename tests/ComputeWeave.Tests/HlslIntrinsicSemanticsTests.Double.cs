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

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(6);

        device.Get().For(1, new TransposeDoubleShader(results, 1.5, 2.5, 3.5, 4.5, 5.5, 6.5));

        AssertAgrees(results, 0.0);
    }

    // A double matrix had no overload at all, square or otherwise, so both shapes are probed. The elements
    // are carried through unchanged, so the two sides have to agree to the last bit
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double m11;
        public readonly double m12;
        public readonly double m13;
        public readonly double m21;
        public readonly double m22;
        public readonly double m23;

        /// <inheritdoc/>
        public void Execute()
        {
            double2x2 square = new(this.m11, this.m12, this.m21, this.m22);
            double2x2 transposedSquare = Hlsl.Transpose(square);

            this.results[0] = new double2(transposedSquare.M12, this.m21);
            this.results[1] = new double2(transposedSquare.M21, this.m12);

            double2x3 wide = new(this.m11, this.m12, this.m13, this.m21, this.m22, this.m23);
            double3x2 transposedWide = Hlsl.Transpose(wide);

            this.results[2] = new double2(transposedWide.M11, this.m11);
            this.results[3] = new double2(transposedWide.M12, this.m21);
            this.results[4] = new double2(transposedWide.M21, this.m12);
            this.results[5] = new double2(transposedWide.M32, this.m23);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeDoubleShapes(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(16);

        device.Get().For(1, new TransposeDoubleShapesShader(results, 10.0));

        AssertAgrees(results, 0.0);
    }

    // Every double shape, one slot each, on the same plan as the unsigned probe
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeDoubleShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double seed;

        /// <inheritdoc/>
        public void Execute()
        {
            double1x1 m1x1 = new(this.seed + 1);
            double1x1 t1x1 = Hlsl.Transpose(m1x1);
            this.results[0] = new double2(t1x1.M11, m1x1.M11);

            double1x2 m1x2 = new(this.seed + 1, this.seed + 2);
            double2x1 t1x2 = Hlsl.Transpose(m1x2);
            this.results[1] = new double2(t1x2.M21, m1x2.M12);

            double1x3 m1x3 = new(this.seed + 1, this.seed + 2, this.seed + 3);
            double3x1 t1x3 = Hlsl.Transpose(m1x3);
            this.results[2] = new double2(t1x3.M31, m1x3.M13);

            double1x4 m1x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            double4x1 t1x4 = Hlsl.Transpose(m1x4);
            this.results[3] = new double2(t1x4.M41, m1x4.M14);

            double2x1 m2x1 = new(this.seed + 1, this.seed + 2);
            double1x2 t2x1 = Hlsl.Transpose(m2x1);
            this.results[4] = new double2(t2x1.M12, m2x1.M21);

            double2x2 m2x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            double2x2 t2x2 = Hlsl.Transpose(m2x2);
            this.results[5] = new double2(t2x2.M21, m2x2.M12);

            double2x3 m2x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6);
            double3x2 t2x3 = Hlsl.Transpose(m2x3);
            this.results[6] = new double2(t2x3.M31, m2x3.M13);

            double2x4 m2x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8);
            double4x2 t2x4 = Hlsl.Transpose(m2x4);
            this.results[7] = new double2(t2x4.M41, m2x4.M14);

            double3x1 m3x1 = new(this.seed + 1, this.seed + 2, this.seed + 3);
            double1x3 t3x1 = Hlsl.Transpose(m3x1);
            this.results[8] = new double2(t3x1.M13, m3x1.M31);

            double3x2 m3x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6);
            double2x3 t3x2 = Hlsl.Transpose(m3x2);
            this.results[9] = new double2(t3x2.M21, m3x2.M12);

            double3x3 m3x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9);
            double3x3 t3x3 = Hlsl.Transpose(m3x3);
            this.results[10] = new double2(t3x3.M31, m3x3.M13);

            double3x4 m3x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12);
            double4x3 t3x4 = Hlsl.Transpose(m3x4);
            this.results[11] = new double2(t3x4.M41, m3x4.M14);

            double4x1 m4x1 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            double1x4 t4x1 = Hlsl.Transpose(m4x1);
            this.results[12] = new double2(t4x1.M14, m4x1.M41);

            double4x2 m4x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8);
            double2x4 t4x2 = Hlsl.Transpose(m4x2);
            this.results[13] = new double2(t4x2.M21, m4x2.M12);

            double4x3 m4x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12);
            double3x4 t4x3 = Hlsl.Transpose(m4x3);
            this.results[14] = new double2(t4x3.M31, m4x3.M13);

            double4x4 m4x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12, this.seed + 13, this.seed + 14, this.seed + 15, this.seed + 16);
            double4x4 t4x4 = Hlsl.Transpose(m4x4);
            this.results[15] = new double2(t4x4.M41, m4x4.M14);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AsDoubleShapes(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(18);

        device.Get().For(1, new AsDoubleShapesShader(results, 0x00000000u, 0x3FF00000u, 1.0));

        AssertAgrees(results, 0.0);
    }

    // Every shape this change added, one slot each. The last element is the one read, so a shape
    // that reinterpreted fewer components than it declares would not agree
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsDoubleShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly uint low;
        public readonly uint high;
        public readonly double expected;

        /// <inheritdoc/>
        public void Execute()
        {
            uint3 low3 = new(this.low, this.low, this.low);
            uint3 high3 = new(this.high, this.high, this.high);
            double3 v3 = Hlsl.AsDouble(low3, high3);
            this.results[0] = new double2(v3.Z, this.expected);

            uint4 low4 = new(this.low, this.low, this.low, this.low);
            uint4 high4 = new(this.high, this.high, this.high, this.high);
            double4 v4 = Hlsl.AsDouble(low4, high4);
            this.results[1] = new double2(v4.W, this.expected);

            uint1x1 low1x1 = new(this.low);
            uint1x1 high1x1 = new(this.high);
            double1x1 v1x1 = Hlsl.AsDouble(low1x1, high1x1);
            this.results[2] = new double2(v1x1.M11, this.expected);

            uint1x2 low1x2 = new(this.low, this.low);
            uint1x2 high1x2 = new(this.high, this.high);
            double1x2 v1x2 = Hlsl.AsDouble(low1x2, high1x2);
            this.results[3] = new double2(v1x2.M12, this.expected);

            uint1x3 low1x3 = new(this.low, this.low, this.low);
            uint1x3 high1x3 = new(this.high, this.high, this.high);
            double1x3 v1x3 = Hlsl.AsDouble(low1x3, high1x3);
            this.results[4] = new double2(v1x3.M13, this.expected);

            uint1x4 low1x4 = new(this.low, this.low, this.low, this.low);
            uint1x4 high1x4 = new(this.high, this.high, this.high, this.high);
            double1x4 v1x4 = Hlsl.AsDouble(low1x4, high1x4);
            this.results[5] = new double2(v1x4.M14, this.expected);

            uint2x1 low2x1 = new(this.low, this.low);
            uint2x1 high2x1 = new(this.high, this.high);
            double2x1 v2x1 = Hlsl.AsDouble(low2x1, high2x1);
            this.results[6] = new double2(v2x1.M21, this.expected);

            uint2x2 low2x2 = new(this.low, this.low, this.low, this.low);
            uint2x2 high2x2 = new(this.high, this.high, this.high, this.high);
            double2x2 v2x2 = Hlsl.AsDouble(low2x2, high2x2);
            this.results[7] = new double2(v2x2.M22, this.expected);

            uint2x3 low2x3 = new(this.low, this.low, this.low, this.low, this.low, this.low);
            uint2x3 high2x3 = new(this.high, this.high, this.high, this.high, this.high, this.high);
            double2x3 v2x3 = Hlsl.AsDouble(low2x3, high2x3);
            this.results[8] = new double2(v2x3.M23, this.expected);

            uint2x4 low2x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint2x4 high2x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double2x4 v2x4 = Hlsl.AsDouble(low2x4, high2x4);
            this.results[9] = new double2(v2x4.M24, this.expected);

            uint3x1 low3x1 = new(this.low, this.low, this.low);
            uint3x1 high3x1 = new(this.high, this.high, this.high);
            double3x1 v3x1 = Hlsl.AsDouble(low3x1, high3x1);
            this.results[10] = new double2(v3x1.M31, this.expected);

            uint3x2 low3x2 = new(this.low, this.low, this.low, this.low, this.low, this.low);
            uint3x2 high3x2 = new(this.high, this.high, this.high, this.high, this.high, this.high);
            double3x2 v3x2 = Hlsl.AsDouble(low3x2, high3x2);
            this.results[11] = new double2(v3x2.M32, this.expected);

            uint3x3 low3x3 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint3x3 high3x3 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double3x3 v3x3 = Hlsl.AsDouble(low3x3, high3x3);
            this.results[12] = new double2(v3x3.M33, this.expected);

            uint3x4 low3x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint3x4 high3x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double3x4 v3x4 = Hlsl.AsDouble(low3x4, high3x4);
            this.results[13] = new double2(v3x4.M34, this.expected);

            uint4x1 low4x1 = new(this.low, this.low, this.low, this.low);
            uint4x1 high4x1 = new(this.high, this.high, this.high, this.high);
            double4x1 v4x1 = Hlsl.AsDouble(low4x1, high4x1);
            this.results[14] = new double2(v4x1.M41, this.expected);

            uint4x2 low4x2 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint4x2 high4x2 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x2 v4x2 = Hlsl.AsDouble(low4x2, high4x2);
            this.results[15] = new double2(v4x2.M42, this.expected);

            uint4x3 low4x3 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint4x3 high4x3 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x3 v4x3 = Hlsl.AsDouble(low4x3, high4x3);
            this.results[16] = new double2(v4x3.M43, this.expected);

            uint4x4 low4x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            uint4x4 high4x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x4 v4x4 = Hlsl.AsDouble(low4x4, high4x4);
            this.results[17] = new double2(v4x4.M44, this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AsDoubleSignedShapes(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(20);

        device.Get().For(1, new AsDoubleSignedShapesShader(results, 0, 0x3FF00000, 1.0));

        AssertAgrees(results, 0.0);
    }

    // Every signed shape this change added, one slot each. The bit pattern is the same one the
    // unsigned probe uses, so the two sides differ only in the declared type of the halves
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsDoubleSignedShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly int low;
        public readonly int high;
        public readonly double expected;

        /// <inheritdoc/>
        public void Execute()
        {
            double scalar = Hlsl.AsDouble(this.low, this.high);
            this.results[0] = new double2(scalar, this.expected);

            int2 low2 = new(this.low, this.low);
            int2 high2 = new(this.high, this.high);
            double2 v2 = Hlsl.AsDouble(low2, high2);
            this.results[1] = new double2(v2.Y, this.expected);

            int3 low3 = new(this.low, this.low, this.low);
            int3 high3 = new(this.high, this.high, this.high);
            double3 v3 = Hlsl.AsDouble(low3, high3);
            this.results[2] = new double2(v3.Z, this.expected);

            int4 low4 = new(this.low, this.low, this.low, this.low);
            int4 high4 = new(this.high, this.high, this.high, this.high);
            double4 v4 = Hlsl.AsDouble(low4, high4);
            this.results[3] = new double2(v4.W, this.expected);

            int1x1 low1x1 = new(this.low);
            int1x1 high1x1 = new(this.high);
            double1x1 v1x1 = Hlsl.AsDouble(low1x1, high1x1);
            this.results[4] = new double2(v1x1.M11, this.expected);

            int1x2 low1x2 = new(this.low, this.low);
            int1x2 high1x2 = new(this.high, this.high);
            double1x2 v1x2 = Hlsl.AsDouble(low1x2, high1x2);
            this.results[5] = new double2(v1x2.M12, this.expected);

            int1x3 low1x3 = new(this.low, this.low, this.low);
            int1x3 high1x3 = new(this.high, this.high, this.high);
            double1x3 v1x3 = Hlsl.AsDouble(low1x3, high1x3);
            this.results[6] = new double2(v1x3.M13, this.expected);

            int1x4 low1x4 = new(this.low, this.low, this.low, this.low);
            int1x4 high1x4 = new(this.high, this.high, this.high, this.high);
            double1x4 v1x4 = Hlsl.AsDouble(low1x4, high1x4);
            this.results[7] = new double2(v1x4.M14, this.expected);

            int2x1 low2x1 = new(this.low, this.low);
            int2x1 high2x1 = new(this.high, this.high);
            double2x1 v2x1 = Hlsl.AsDouble(low2x1, high2x1);
            this.results[8] = new double2(v2x1.M21, this.expected);

            int2x2 low2x2 = new(this.low, this.low, this.low, this.low);
            int2x2 high2x2 = new(this.high, this.high, this.high, this.high);
            double2x2 v2x2 = Hlsl.AsDouble(low2x2, high2x2);
            this.results[9] = new double2(v2x2.M22, this.expected);

            int2x3 low2x3 = new(this.low, this.low, this.low, this.low, this.low, this.low);
            int2x3 high2x3 = new(this.high, this.high, this.high, this.high, this.high, this.high);
            double2x3 v2x3 = Hlsl.AsDouble(low2x3, high2x3);
            this.results[10] = new double2(v2x3.M23, this.expected);

            int2x4 low2x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int2x4 high2x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double2x4 v2x4 = Hlsl.AsDouble(low2x4, high2x4);
            this.results[11] = new double2(v2x4.M24, this.expected);

            int3x1 low3x1 = new(this.low, this.low, this.low);
            int3x1 high3x1 = new(this.high, this.high, this.high);
            double3x1 v3x1 = Hlsl.AsDouble(low3x1, high3x1);
            this.results[12] = new double2(v3x1.M31, this.expected);

            int3x2 low3x2 = new(this.low, this.low, this.low, this.low, this.low, this.low);
            int3x2 high3x2 = new(this.high, this.high, this.high, this.high, this.high, this.high);
            double3x2 v3x2 = Hlsl.AsDouble(low3x2, high3x2);
            this.results[13] = new double2(v3x2.M32, this.expected);

            int3x3 low3x3 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int3x3 high3x3 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double3x3 v3x3 = Hlsl.AsDouble(low3x3, high3x3);
            this.results[14] = new double2(v3x3.M33, this.expected);

            int3x4 low3x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int3x4 high3x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double3x4 v3x4 = Hlsl.AsDouble(low3x4, high3x4);
            this.results[15] = new double2(v3x4.M34, this.expected);

            int4x1 low4x1 = new(this.low, this.low, this.low, this.low);
            int4x1 high4x1 = new(this.high, this.high, this.high, this.high);
            double4x1 v4x1 = Hlsl.AsDouble(low4x1, high4x1);
            this.results[16] = new double2(v4x1.M41, this.expected);

            int4x2 low4x2 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int4x2 high4x2 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x2 v4x2 = Hlsl.AsDouble(low4x2, high4x2);
            this.results[17] = new double2(v4x2.M42, this.expected);

            int4x3 low4x3 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int4x3 high4x3 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x3 v4x3 = Hlsl.AsDouble(low4x3, high4x3);
            this.results[18] = new double2(v4x3.M43, this.expected);

            int4x4 low4x4 = new(this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low, this.low);
            int4x4 high4x4 = new(this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high, this.high);
            double4x4 v4x4 = Hlsl.AsDouble(low4x4, high4x4);
            this.results[19] = new double2(v4x4.M44, this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_ClampAndExtremesDouble(Device device)
    {
        if (!device.Get().IsDoublePrecisionSupportAvailable())
        {
            Assert.Inconclusive();
        }

        using ReadWriteBuffer<double2> results = device.Get().AllocateReadWriteBuffer<double2>(60);

        device.Get().For(1, new ClampAndExtremesDoubleShader(results, 5.0, 2.0, 8.0));

        AssertAgrees(results, 0.0);
    }

    // The double shapes of Clamp, on the same plan as the unsigned probe. Max and Min already
    // accepted a double, so only the clamped slots are new here
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [RequiresDoublePrecisionSupport]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ClampAndExtremesDoubleShader : IComputeShader
    {
        public readonly ReadWriteBuffer<double2> results;
        public readonly double middle;
        public readonly double lowest;
        public readonly double highest;

        /// <inheritdoc/>
        public void Execute()
        {
            double midscalar = this.middle;
            double loscalar = this.lowest;
            double hiscalar = this.highest;
            double clampedscalar = Hlsl.Clamp(midscalar, loscalar, hiscalar);
            double biggestscalar = Hlsl.Max(loscalar, hiscalar);
            double smallestscalar = Hlsl.Min(loscalar, hiscalar);
            this.results[0] = new double2(clampedscalar, this.middle);
            this.results[1] = new double2(biggestscalar, this.highest);
            this.results[2] = new double2(smallestscalar, this.lowest);

            double2 midv2 = new(this.middle, this.middle);
            double2 lov2 = new(this.lowest, this.lowest);
            double2 hiv2 = new(this.highest, this.highest);
            double2 clampedv2 = Hlsl.Clamp(midv2, lov2, hiv2);
            double2 biggestv2 = Hlsl.Max(lov2, hiv2);
            double2 smallestv2 = Hlsl.Min(lov2, hiv2);
            this.results[3] = new double2(clampedv2.Y, this.middle);
            this.results[4] = new double2(biggestv2.Y, this.highest);
            this.results[5] = new double2(smallestv2.Y, this.lowest);

            double3 midv3 = new(this.middle, this.middle, this.middle);
            double3 lov3 = new(this.lowest, this.lowest, this.lowest);
            double3 hiv3 = new(this.highest, this.highest, this.highest);
            double3 clampedv3 = Hlsl.Clamp(midv3, lov3, hiv3);
            double3 biggestv3 = Hlsl.Max(lov3, hiv3);
            double3 smallestv3 = Hlsl.Min(lov3, hiv3);
            this.results[6] = new double2(clampedv3.Z, this.middle);
            this.results[7] = new double2(biggestv3.Z, this.highest);
            this.results[8] = new double2(smallestv3.Z, this.lowest);

            double4 midv4 = new(this.middle, this.middle, this.middle, this.middle);
            double4 lov4 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            double4 hiv4 = new(this.highest, this.highest, this.highest, this.highest);
            double4 clampedv4 = Hlsl.Clamp(midv4, lov4, hiv4);
            double4 biggestv4 = Hlsl.Max(lov4, hiv4);
            double4 smallestv4 = Hlsl.Min(lov4, hiv4);
            this.results[9] = new double2(clampedv4.W, this.middle);
            this.results[10] = new double2(biggestv4.W, this.highest);
            this.results[11] = new double2(smallestv4.W, this.lowest);

            double1x1 midm1x1 = new(this.middle);
            double1x1 lom1x1 = new(this.lowest);
            double1x1 him1x1 = new(this.highest);
            double1x1 clampedm1x1 = Hlsl.Clamp(midm1x1, lom1x1, him1x1);
            double1x1 biggestm1x1 = Hlsl.Max(lom1x1, him1x1);
            double1x1 smallestm1x1 = Hlsl.Min(lom1x1, him1x1);
            this.results[12] = new double2(clampedm1x1.M11, this.middle);
            this.results[13] = new double2(biggestm1x1.M11, this.highest);
            this.results[14] = new double2(smallestm1x1.M11, this.lowest);

            double1x2 midm1x2 = new(this.middle, this.middle);
            double1x2 lom1x2 = new(this.lowest, this.lowest);
            double1x2 him1x2 = new(this.highest, this.highest);
            double1x2 clampedm1x2 = Hlsl.Clamp(midm1x2, lom1x2, him1x2);
            double1x2 biggestm1x2 = Hlsl.Max(lom1x2, him1x2);
            double1x2 smallestm1x2 = Hlsl.Min(lom1x2, him1x2);
            this.results[15] = new double2(clampedm1x2.M12, this.middle);
            this.results[16] = new double2(biggestm1x2.M12, this.highest);
            this.results[17] = new double2(smallestm1x2.M12, this.lowest);

            double1x3 midm1x3 = new(this.middle, this.middle, this.middle);
            double1x3 lom1x3 = new(this.lowest, this.lowest, this.lowest);
            double1x3 him1x3 = new(this.highest, this.highest, this.highest);
            double1x3 clampedm1x3 = Hlsl.Clamp(midm1x3, lom1x3, him1x3);
            double1x3 biggestm1x3 = Hlsl.Max(lom1x3, him1x3);
            double1x3 smallestm1x3 = Hlsl.Min(lom1x3, him1x3);
            this.results[18] = new double2(clampedm1x3.M13, this.middle);
            this.results[19] = new double2(biggestm1x3.M13, this.highest);
            this.results[20] = new double2(smallestm1x3.M13, this.lowest);

            double1x4 midm1x4 = new(this.middle, this.middle, this.middle, this.middle);
            double1x4 lom1x4 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            double1x4 him1x4 = new(this.highest, this.highest, this.highest, this.highest);
            double1x4 clampedm1x4 = Hlsl.Clamp(midm1x4, lom1x4, him1x4);
            double1x4 biggestm1x4 = Hlsl.Max(lom1x4, him1x4);
            double1x4 smallestm1x4 = Hlsl.Min(lom1x4, him1x4);
            this.results[21] = new double2(clampedm1x4.M14, this.middle);
            this.results[22] = new double2(biggestm1x4.M14, this.highest);
            this.results[23] = new double2(smallestm1x4.M14, this.lowest);

            double2x1 midm2x1 = new(this.middle, this.middle);
            double2x1 lom2x1 = new(this.lowest, this.lowest);
            double2x1 him2x1 = new(this.highest, this.highest);
            double2x1 clampedm2x1 = Hlsl.Clamp(midm2x1, lom2x1, him2x1);
            double2x1 biggestm2x1 = Hlsl.Max(lom2x1, him2x1);
            double2x1 smallestm2x1 = Hlsl.Min(lom2x1, him2x1);
            this.results[24] = new double2(clampedm2x1.M21, this.middle);
            this.results[25] = new double2(biggestm2x1.M21, this.highest);
            this.results[26] = new double2(smallestm2x1.M21, this.lowest);

            double2x2 midm2x2 = new(this.middle, this.middle, this.middle, this.middle);
            double2x2 lom2x2 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            double2x2 him2x2 = new(this.highest, this.highest, this.highest, this.highest);
            double2x2 clampedm2x2 = Hlsl.Clamp(midm2x2, lom2x2, him2x2);
            double2x2 biggestm2x2 = Hlsl.Max(lom2x2, him2x2);
            double2x2 smallestm2x2 = Hlsl.Min(lom2x2, him2x2);
            this.results[27] = new double2(clampedm2x2.M22, this.middle);
            this.results[28] = new double2(biggestm2x2.M22, this.highest);
            this.results[29] = new double2(smallestm2x2.M22, this.lowest);

            double2x3 midm2x3 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double2x3 lom2x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double2x3 him2x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double2x3 clampedm2x3 = Hlsl.Clamp(midm2x3, lom2x3, him2x3);
            double2x3 biggestm2x3 = Hlsl.Max(lom2x3, him2x3);
            double2x3 smallestm2x3 = Hlsl.Min(lom2x3, him2x3);
            this.results[30] = new double2(clampedm2x3.M23, this.middle);
            this.results[31] = new double2(biggestm2x3.M23, this.highest);
            this.results[32] = new double2(smallestm2x3.M23, this.lowest);

            double2x4 midm2x4 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double2x4 lom2x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double2x4 him2x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double2x4 clampedm2x4 = Hlsl.Clamp(midm2x4, lom2x4, him2x4);
            double2x4 biggestm2x4 = Hlsl.Max(lom2x4, him2x4);
            double2x4 smallestm2x4 = Hlsl.Min(lom2x4, him2x4);
            this.results[33] = new double2(clampedm2x4.M24, this.middle);
            this.results[34] = new double2(biggestm2x4.M24, this.highest);
            this.results[35] = new double2(smallestm2x4.M24, this.lowest);

            double3x1 midm3x1 = new(this.middle, this.middle, this.middle);
            double3x1 lom3x1 = new(this.lowest, this.lowest, this.lowest);
            double3x1 him3x1 = new(this.highest, this.highest, this.highest);
            double3x1 clampedm3x1 = Hlsl.Clamp(midm3x1, lom3x1, him3x1);
            double3x1 biggestm3x1 = Hlsl.Max(lom3x1, him3x1);
            double3x1 smallestm3x1 = Hlsl.Min(lom3x1, him3x1);
            this.results[36] = new double2(clampedm3x1.M31, this.middle);
            this.results[37] = new double2(biggestm3x1.M31, this.highest);
            this.results[38] = new double2(smallestm3x1.M31, this.lowest);

            double3x2 midm3x2 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double3x2 lom3x2 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double3x2 him3x2 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double3x2 clampedm3x2 = Hlsl.Clamp(midm3x2, lom3x2, him3x2);
            double3x2 biggestm3x2 = Hlsl.Max(lom3x2, him3x2);
            double3x2 smallestm3x2 = Hlsl.Min(lom3x2, him3x2);
            this.results[39] = new double2(clampedm3x2.M32, this.middle);
            this.results[40] = new double2(biggestm3x2.M32, this.highest);
            this.results[41] = new double2(smallestm3x2.M32, this.lowest);

            double3x3 midm3x3 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double3x3 lom3x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double3x3 him3x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double3x3 clampedm3x3 = Hlsl.Clamp(midm3x3, lom3x3, him3x3);
            double3x3 biggestm3x3 = Hlsl.Max(lom3x3, him3x3);
            double3x3 smallestm3x3 = Hlsl.Min(lom3x3, him3x3);
            this.results[42] = new double2(clampedm3x3.M33, this.middle);
            this.results[43] = new double2(biggestm3x3.M33, this.highest);
            this.results[44] = new double2(smallestm3x3.M33, this.lowest);

            double3x4 midm3x4 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double3x4 lom3x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double3x4 him3x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double3x4 clampedm3x4 = Hlsl.Clamp(midm3x4, lom3x4, him3x4);
            double3x4 biggestm3x4 = Hlsl.Max(lom3x4, him3x4);
            double3x4 smallestm3x4 = Hlsl.Min(lom3x4, him3x4);
            this.results[45] = new double2(clampedm3x4.M34, this.middle);
            this.results[46] = new double2(biggestm3x4.M34, this.highest);
            this.results[47] = new double2(smallestm3x4.M34, this.lowest);

            double4x1 midm4x1 = new(this.middle, this.middle, this.middle, this.middle);
            double4x1 lom4x1 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            double4x1 him4x1 = new(this.highest, this.highest, this.highest, this.highest);
            double4x1 clampedm4x1 = Hlsl.Clamp(midm4x1, lom4x1, him4x1);
            double4x1 biggestm4x1 = Hlsl.Max(lom4x1, him4x1);
            double4x1 smallestm4x1 = Hlsl.Min(lom4x1, him4x1);
            this.results[48] = new double2(clampedm4x1.M41, this.middle);
            this.results[49] = new double2(biggestm4x1.M41, this.highest);
            this.results[50] = new double2(smallestm4x1.M41, this.lowest);

            double4x2 midm4x2 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double4x2 lom4x2 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double4x2 him4x2 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double4x2 clampedm4x2 = Hlsl.Clamp(midm4x2, lom4x2, him4x2);
            double4x2 biggestm4x2 = Hlsl.Max(lom4x2, him4x2);
            double4x2 smallestm4x2 = Hlsl.Min(lom4x2, him4x2);
            this.results[51] = new double2(clampedm4x2.M42, this.middle);
            this.results[52] = new double2(biggestm4x2.M42, this.highest);
            this.results[53] = new double2(smallestm4x2.M42, this.lowest);

            double4x3 midm4x3 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double4x3 lom4x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double4x3 him4x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double4x3 clampedm4x3 = Hlsl.Clamp(midm4x3, lom4x3, him4x3);
            double4x3 biggestm4x3 = Hlsl.Max(lom4x3, him4x3);
            double4x3 smallestm4x3 = Hlsl.Min(lom4x3, him4x3);
            this.results[54] = new double2(clampedm4x3.M43, this.middle);
            this.results[55] = new double2(biggestm4x3.M43, this.highest);
            this.results[56] = new double2(smallestm4x3.M43, this.lowest);

            double4x4 midm4x4 = new(this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle, this.middle);
            double4x4 lom4x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            double4x4 him4x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            double4x4 clampedm4x4 = Hlsl.Clamp(midm4x4, lom4x4, him4x4);
            double4x4 biggestm4x4 = Hlsl.Max(lom4x4, him4x4);
            double4x4 smallestm4x4 = Hlsl.Min(lom4x4, him4x4);
            this.results[57] = new double2(clampedm4x4.M44, this.middle);
            this.results[58] = new double2(biggestm4x4.M44, this.highest);
            this.results[59] = new double2(smallestm4x4.M44, this.lowest);
        }
    }
}
