using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <inheritdoc/>
partial class HlslIntrinsicSemanticsTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Frexp(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new FrexpShader(results, 48.0f, 0.75f));

        AssertAgrees(results, 0.001f);
    }

    // 48 splits into a mantissa of 0.75 and an exponent of 6. The first slot puts the two halves back
    // together, the second pins which half the call returns and which one it writes through the argument
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FrexpShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;
        public readonly float expectedMantissa;

        /// <inheritdoc/>
        public void Execute()
        {
            float mantissa = Hlsl.Frexp(this.x, out float exponent);

            this.results[0] = new float2(mantissa * Hlsl.Exp2(exponent), this.x);
            this.results[1] = new float2(mantissa, this.expectedMantissa);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Modf(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(4);

        device.Get().For(1, new ModfShader(results, 3.75f, -3.75f, -3.0f));

        AssertAgrees(results, 0.0f);
    }

    // The two parts must add back to the input, and the written one must be the integer part. A negative
    // input is probed as well: the split runs toward zero, so the integer part of -3.75 is -3 and not -4.
    // Comparing it against the truncation of a positive number would not have shown that
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ModfShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float positive;
        public readonly float negative;
        public readonly float expectedNegativeIntegral;

        /// <inheritdoc/>
        public void Execute()
        {
            float positiveFraction = Hlsl.Modf(this.positive, out float positiveIntegral);
            float negativeFraction = Hlsl.Modf(this.negative, out float negativeIntegral);

            this.results[0] = new float2(positiveFraction + positiveIntegral, this.positive);
            this.results[1] = new float2(positiveIntegral, Hlsl.Floor(this.positive));
            this.results[2] = new float2(negativeFraction + negativeIntegral, this.negative);
            this.results[3] = new float2(negativeIntegral, this.expectedNegativeIntegral);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_SinCos(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new SinCosShader(results, 0.7f));

        AssertAgrees(results, 0.000001f);
    }

    // the two written arguments are in sine then cosine order, which the pair of slots separates
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SinCosShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            Hlsl.SinCos(this.x, out float sine, out float cosine);

            this.results[0] = new float2(sine, Hlsl.Sin(this.x));
            this.results[1] = new float2(cosine, Hlsl.Cos(this.x));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Refract(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new RefractShader(results, 0.6f, 1.0f, 0.5f));

        AssertAgrees(results, 0.00001f);
    }

    // the three arguments carry different roles, so writing the documented formula out pins their order
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct RefractShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float incident;
        public readonly float normal;
        public readonly float index;

        /// <inheritdoc/>
        public void Execute()
        {
            float projection = this.normal * this.incident;
            float k = 1.0f - (this.index * this.index * (1.0f - (projection * projection)));
            float expected = k < 0.0f
                ? 0.0f
                : (this.index * this.incident) - (((this.index * projection) + Hlsl.Sqrt(k)) * this.normal);

            this.results[0] = new float2(Hlsl.Refract(this.incident, this.normal, this.index), expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Determinant(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new DeterminantShader(results, 1.0f, 2.0f, 3.0f, 4.0f));

        AssertAgrees(results, 0.000001f);
    }

    // the determinant of a two by two matrix is the difference of its two diagonal products, and it does not
    // depend on whether the matrix is read by rows or by columns
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DeterminantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float m11;
        public readonly float m12;
        public readonly float m21;
        public readonly float m22;

        /// <inheritdoc/>
        public void Execute()
        {
            float2x2 matrix = new(this.m11, this.m12, this.m21, this.m22);

            this.results[0] = new float2(Hlsl.Determinant(matrix), (this.m11 * this.m22) - (this.m12 * this.m21));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Transpose(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(4);

        device.Get().For(1, new TransposeShader(results, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f));

        AssertAgrees(results, 0.0f);
    }

    // A two by three matrix covers the non-square shape. Every element is distinct, so each slot pins one
    // position of the result against the position it must have come from
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float m11;
        public readonly float m12;
        public readonly float m13;
        public readonly float m21;
        public readonly float m22;
        public readonly float m23;

        /// <inheritdoc/>
        public void Execute()
        {
            float2x3 matrix = new(this.m11, this.m12, this.m13, this.m21, this.m22, this.m23);
            float3x2 transposed = Hlsl.Transpose(matrix);

            this.results[0] = new float2(transposed.M11, this.m11);
            this.results[1] = new float2(transposed.M12, this.m21);
            this.results[2] = new float2(transposed.M21, this.m12);
            this.results[3] = new float2(transposed.M32, this.m23);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeSquare(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(13);

        device.Get().For(1, new TransposeSquareShader(results, 1.0f, 2.0f, 3.0f, 4.0f));

        AssertAgrees(results, 0.0f);
    }

    // A square shape is the case the intrinsic had no overload for. Three by three catches a transpose
    // confined to the leading block, and one by one only pins that the shape is accepted and runs, its
    // transpose being itself
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeSquareShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float m11;
        public readonly float m12;
        public readonly float m21;
        public readonly float m22;

        /// <inheritdoc/>
        public void Execute()
        {
            float2x2 square2 = new(this.m11, this.m12, this.m21, this.m22);
            float2x2 transposed2 = Hlsl.Transpose(square2);

            this.results[0] = new float2(transposed2.M11, this.m11);
            this.results[1] = new float2(transposed2.M12, this.m21);
            this.results[2] = new float2(transposed2.M21, this.m12);
            this.results[3] = new float2(transposed2.M22, this.m22);

            float3x3 square3 = new(
                this.m11, this.m12, this.m21,
                this.m22, this.m11 + this.m22, this.m12 + this.m21,
                this.m11 - this.m22, this.m12 - this.m21, this.m21 - this.m11);
            float3x3 transposed3 = Hlsl.Transpose(square3);

            this.results[4] = new float2(transposed3.M13, square3.M31);
            this.results[5] = new float2(transposed3.M31, square3.M13);
            this.results[6] = new float2(transposed3.M23, square3.M32);
            this.results[7] = new float2(transposed3.M32, square3.M23);

            float1x1 square1 = new(this.m11);
            float1x1 transposed1 = Hlsl.Transpose(square1);

            this.results[8] = new float2(transposed1.M11, this.m11);

            float4x4 square4 = new(
                this.m11, this.m12, this.m21, this.m22,
                this.m11 + 4.0f, this.m12 + 4.0f, this.m21 + 4.0f, this.m22 + 4.0f,
                this.m11 + 8.0f, this.m12 + 8.0f, this.m21 + 8.0f, this.m22 + 8.0f,
                this.m11 + 12.0f, this.m12 + 12.0f, this.m21 + 12.0f, this.m22 + 12.0f);
            float4x4 transposed4 = Hlsl.Transpose(square4);

            this.results[9] = new float2(transposed4.M14, square4.M41);
            this.results[10] = new float2(transposed4.M41, square4.M14);
            this.results[11] = new float2(transposed4.M23, square4.M32);
            this.results[12] = new float2(transposed4.M32, square4.M23);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeUInt(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(6);

        device.Get().For(1, new TransposeUIntShader(results, 11u, 22u, 33u, 44u, 55u, 66u));

        AssertAgreesExactly(results);
    }

    // An unsigned matrix had no overload at all, square or otherwise, so both shapes are probed. The
    // comparison is exact because the elements travel through the transpose without arithmetic
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeUIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint m11;
        public readonly uint m12;
        public readonly uint m13;
        public readonly uint m21;
        public readonly uint m22;
        public readonly uint m23;

        /// <inheritdoc/>
        public void Execute()
        {
            uint2x2 square = new(this.m11, this.m12, this.m21, this.m22);
            uint2x2 transposedSquare = Hlsl.Transpose(square);

            this.results[0] = new uint2(transposedSquare.M12, this.m21);
            this.results[1] = new uint2(transposedSquare.M21, this.m12);

            uint2x3 wide = new(this.m11, this.m12, this.m13, this.m21, this.m22, this.m23);
            uint3x2 transposedWide = Hlsl.Transpose(wide);

            this.results[2] = new uint2(transposedWide.M11, this.m11);
            this.results[3] = new uint2(transposedWide.M12, this.m21);
            this.results[4] = new uint2(transposedWide.M21, this.m12);
            this.results[5] = new uint2(transposedWide.M32, this.m23);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeInt(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(5);

        device.Get().For(1, new TransposeIntShader(results, 1, 2, 3, 4, 5, 6));

        AssertAgrees(results, 0.0f);
    }

    // A signed matrix had no square overload, and no shape of it had ever been run through the intrinsic,
    // so both a square and a non-square shape are probed here
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly int m11;
        public readonly int m12;
        public readonly int m13;
        public readonly int m21;
        public readonly int m22;
        public readonly int m23;

        /// <inheritdoc/>
        public void Execute()
        {
            int2x2 square = new(this.m11, this.m12, this.m21, this.m22);
            int2x2 transposedSquare = Hlsl.Transpose(square);

            this.results[0] = new float2(transposedSquare.M12, this.m21);
            this.results[1] = new float2(transposedSquare.M21, this.m12);

            int2x3 wide = new(this.m11, this.m12, this.m13, this.m21, this.m22, this.m23);
            int3x2 transposedWide = Hlsl.Transpose(wide);

            this.results[2] = new float2(transposedWide.M12, this.m21);
            this.results[3] = new float2(transposedWide.M21, this.m12);
            this.results[4] = new float2(transposedWide.M32, this.m23);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeBool(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(8);

        device.Get().For(1, new TransposeBoolShader(results, 1.0f));

        AssertAgrees(results, 0.0f);
    }

    // A boolean matrix had no square overload either, and had never been run. Both sides of a slot go
    // through the same conversion, so the last two slots pin that conversion against literals
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeBoolShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float seed;

        /// <inheritdoc/>
        public void Execute()
        {
            bool yes = this.seed > 0.0f;
            bool no = this.seed < 0.0f;

            bool2x2 square = new(yes, no, yes, yes);
            bool2x2 transposedSquare = Hlsl.Transpose(square);
            float2x2 squareAsFloat = Hlsl.BoolToFloat(square);
            float2x2 transposedAsFloat = Hlsl.BoolToFloat(transposedSquare);

            this.results[0] = new float2(transposedAsFloat.M12, squareAsFloat.M21);
            this.results[1] = new float2(transposedAsFloat.M21, squareAsFloat.M12);

            bool2x3 wide = new(yes, no, no, no, yes, yes);
            bool3x2 transposedWide = Hlsl.Transpose(wide);
            float2x3 wideAsFloat = Hlsl.BoolToFloat(wide);
            float3x2 transposedWideAsFloat = Hlsl.BoolToFloat(transposedWide);

            this.results[2] = new float2(transposedWideAsFloat.M11, wideAsFloat.M11);
            this.results[3] = new float2(transposedWideAsFloat.M12, wideAsFloat.M21);
            this.results[4] = new float2(transposedWideAsFloat.M21, wideAsFloat.M12);
            this.results[5] = new float2(transposedWideAsFloat.M32, wideAsFloat.M23);

            this.results[6] = new float2(squareAsFloat.M11, 1.0f);
            this.results[7] = new float2(squareAsFloat.M12, 0.0f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeUIntShapes(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(16);

        device.Get().For(1, new TransposeUIntShapesShader(results, 10u));

        AssertAgreesExactly(results);
    }

    // Every unsigned shape, one slot each. The elements are consecutive, so the slot disagrees
    // unless the element travelled from the position the transpose says it came from
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeUIntShapesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint seed;

        /// <inheritdoc/>
        public void Execute()
        {
            uint1x1 m1x1 = new(this.seed + 1);
            uint1x1 t1x1 = Hlsl.Transpose(m1x1);
            this.results[0] = new uint2(t1x1.M11, m1x1.M11);

            uint1x2 m1x2 = new(this.seed + 1, this.seed + 2);
            uint2x1 t1x2 = Hlsl.Transpose(m1x2);
            this.results[1] = new uint2(t1x2.M21, m1x2.M12);

            uint1x3 m1x3 = new(this.seed + 1, this.seed + 2, this.seed + 3);
            uint3x1 t1x3 = Hlsl.Transpose(m1x3);
            this.results[2] = new uint2(t1x3.M31, m1x3.M13);

            uint1x4 m1x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            uint4x1 t1x4 = Hlsl.Transpose(m1x4);
            this.results[3] = new uint2(t1x4.M41, m1x4.M14);

            uint2x1 m2x1 = new(this.seed + 1, this.seed + 2);
            uint1x2 t2x1 = Hlsl.Transpose(m2x1);
            this.results[4] = new uint2(t2x1.M12, m2x1.M21);

            uint2x2 m2x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            uint2x2 t2x2 = Hlsl.Transpose(m2x2);
            this.results[5] = new uint2(t2x2.M21, m2x2.M12);

            uint2x3 m2x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6);
            uint3x2 t2x3 = Hlsl.Transpose(m2x3);
            this.results[6] = new uint2(t2x3.M31, m2x3.M13);

            uint2x4 m2x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8);
            uint4x2 t2x4 = Hlsl.Transpose(m2x4);
            this.results[7] = new uint2(t2x4.M41, m2x4.M14);

            uint3x1 m3x1 = new(this.seed + 1, this.seed + 2, this.seed + 3);
            uint1x3 t3x1 = Hlsl.Transpose(m3x1);
            this.results[8] = new uint2(t3x1.M13, m3x1.M31);

            uint3x2 m3x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6);
            uint2x3 t3x2 = Hlsl.Transpose(m3x2);
            this.results[9] = new uint2(t3x2.M21, m3x2.M12);

            uint3x3 m3x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9);
            uint3x3 t3x3 = Hlsl.Transpose(m3x3);
            this.results[10] = new uint2(t3x3.M31, m3x3.M13);

            uint3x4 m3x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12);
            uint4x3 t3x4 = Hlsl.Transpose(m3x4);
            this.results[11] = new uint2(t3x4.M41, m3x4.M14);

            uint4x1 m4x1 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            uint1x4 t4x1 = Hlsl.Transpose(m4x1);
            this.results[12] = new uint2(t4x1.M14, m4x1.M41);

            uint4x2 m4x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8);
            uint2x4 t4x2 = Hlsl.Transpose(m4x2);
            this.results[13] = new uint2(t4x2.M21, m4x2.M12);

            uint4x3 m4x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12);
            uint3x4 t4x3 = Hlsl.Transpose(m4x3);
            this.results[14] = new uint2(t4x3.M31, m4x3.M13);

            uint4x4 m4x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12, this.seed + 13, this.seed + 14, this.seed + 15, this.seed + 16);
            uint4x4 t4x4 = Hlsl.Transpose(m4x4);
            this.results[15] = new uint2(t4x4.M41, m4x4.M14);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeIntSquares(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(4);

        device.Get().For(1, new TransposeIntSquaresShader(results, 10));

        AssertAgrees(results, 0.0f);
    }

    // The signed squares, which are the shapes this change added for that kind
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeIntSquaresShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly int seed;

        /// <inheritdoc/>
        public void Execute()
        {
            int1x1 m1x1 = new(this.seed + 1);
            int1x1 t1x1 = Hlsl.Transpose(m1x1);
            this.results[0] = new float2(t1x1.M11, m1x1.M11);

            int2x2 m2x2 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4);
            int2x2 t2x2 = Hlsl.Transpose(m2x2);
            this.results[1] = new float2(t2x2.M21, m2x2.M12);

            int3x3 m3x3 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9);
            int3x3 t3x3 = Hlsl.Transpose(m3x3);
            this.results[2] = new float2(t3x3.M31, m3x3.M13);

            int4x4 m4x4 = new(this.seed + 1, this.seed + 2, this.seed + 3, this.seed + 4, this.seed + 5, this.seed + 6, this.seed + 7, this.seed + 8, this.seed + 9, this.seed + 10, this.seed + 11, this.seed + 12, this.seed + 13, this.seed + 14, this.seed + 15, this.seed + 16);
            int4x4 t4x4 = Hlsl.Transpose(m4x4);
            this.results[3] = new float2(t4x4.M41, m4x4.M14);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_TransposeBoolSquares(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(4);

        device.Get().For(1, new TransposeBoolSquaresShader(results, 1.0f));

        AssertAgrees(results, 0.0f);
    }

    // The boolean squares. A two-valued element cannot be made distinct, so an upper triangle of
    // true over a lower triangle of false is what makes a position tell itself apart
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TransposeBoolSquaresShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float seed;

        /// <inheritdoc/>
        public void Execute()
        {
            bool yes = this.seed > 0.0f;
            bool no = this.seed < 0.0f;

            bool1x1 m1x1 = new(yes);
            bool1x1 t1x1 = Hlsl.Transpose(m1x1);
            float1x1 t1x1AsFloat = Hlsl.BoolToFloat(t1x1);
            float1x1 m1x1AsFloat = Hlsl.BoolToFloat(m1x1);
            this.results[0] = new float2(t1x1AsFloat.M11, m1x1AsFloat.M11);

            bool2x2 m2x2 = new(yes, yes, no, yes);
            bool2x2 t2x2 = Hlsl.Transpose(m2x2);
            float2x2 t2x2AsFloat = Hlsl.BoolToFloat(t2x2);
            float2x2 m2x2AsFloat = Hlsl.BoolToFloat(m2x2);
            this.results[1] = new float2(t2x2AsFloat.M21, m2x2AsFloat.M12);

            bool3x3 m3x3 = new(yes, yes, yes, no, yes, yes, no, no, yes);
            bool3x3 t3x3 = Hlsl.Transpose(m3x3);
            float3x3 t3x3AsFloat = Hlsl.BoolToFloat(t3x3);
            float3x3 m3x3AsFloat = Hlsl.BoolToFloat(m3x3);
            this.results[2] = new float2(t3x3AsFloat.M31, m3x3AsFloat.M13);

            bool4x4 m4x4 = new(yes, yes, yes, yes, no, yes, yes, yes, no, no, yes, yes, no, no, no, yes);
            bool4x4 t4x4 = Hlsl.Transpose(m4x4);
            float4x4 t4x4AsFloat = Hlsl.BoolToFloat(t4x4);
            float4x4 m4x4AsFloat = Hlsl.BoolToFloat(m4x4);
            this.results[3] = new float2(t4x4AsFloat.M41, m4x4AsFloat.M14);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_ClampAndExtremesUnsigned(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(60);

        device.Get().For(1, new ClampAndExtremesUnsignedShader(results, 12u, 2u, 8u));

        AssertAgreesExactly(results);
    }

    // Every unsigned shape of the three, one slot each per intrinsic. The value sits above the
    // upper bound, so a clamp that did nothing would disagree with the bound it must return
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ClampAndExtremesUnsignedShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint outside;
        public readonly uint lowest;
        public readonly uint highest;

        /// <inheritdoc/>
        public void Execute()
        {
            uint outsidescalar = this.outside;
            uint loscalar = this.lowest;
            uint hiscalar = this.highest;
            uint clampedscalar = Hlsl.Clamp(outsidescalar, loscalar, hiscalar);
            uint biggestscalar = Hlsl.Max(loscalar, hiscalar);
            uint smallestscalar = Hlsl.Min(loscalar, hiscalar);
            this.results[0] = new uint2(clampedscalar, this.highest);
            this.results[1] = new uint2(biggestscalar, this.highest);
            this.results[2] = new uint2(smallestscalar, this.lowest);

            uint2 outsidev2 = new(this.outside, this.outside);
            uint2 lov2 = new(this.lowest, this.lowest);
            uint2 hiv2 = new(this.highest, this.highest);
            uint2 clampedv2 = Hlsl.Clamp(outsidev2, lov2, hiv2);
            uint2 biggestv2 = Hlsl.Max(lov2, hiv2);
            uint2 smallestv2 = Hlsl.Min(lov2, hiv2);
            this.results[3] = new uint2(clampedv2.Y, this.highest);
            this.results[4] = new uint2(biggestv2.Y, this.highest);
            this.results[5] = new uint2(smallestv2.Y, this.lowest);

            uint3 outsidev3 = new(this.outside, this.outside, this.outside);
            uint3 lov3 = new(this.lowest, this.lowest, this.lowest);
            uint3 hiv3 = new(this.highest, this.highest, this.highest);
            uint3 clampedv3 = Hlsl.Clamp(outsidev3, lov3, hiv3);
            uint3 biggestv3 = Hlsl.Max(lov3, hiv3);
            uint3 smallestv3 = Hlsl.Min(lov3, hiv3);
            this.results[6] = new uint2(clampedv3.Z, this.highest);
            this.results[7] = new uint2(biggestv3.Z, this.highest);
            this.results[8] = new uint2(smallestv3.Z, this.lowest);

            uint4 outsidev4 = new(this.outside, this.outside, this.outside, this.outside);
            uint4 lov4 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            uint4 hiv4 = new(this.highest, this.highest, this.highest, this.highest);
            uint4 clampedv4 = Hlsl.Clamp(outsidev4, lov4, hiv4);
            uint4 biggestv4 = Hlsl.Max(lov4, hiv4);
            uint4 smallestv4 = Hlsl.Min(lov4, hiv4);
            this.results[9] = new uint2(clampedv4.W, this.highest);
            this.results[10] = new uint2(biggestv4.W, this.highest);
            this.results[11] = new uint2(smallestv4.W, this.lowest);

            uint1x1 outsidem1x1 = new(this.outside);
            uint1x1 lom1x1 = new(this.lowest);
            uint1x1 him1x1 = new(this.highest);
            uint1x1 clampedm1x1 = Hlsl.Clamp(outsidem1x1, lom1x1, him1x1);
            uint1x1 biggestm1x1 = Hlsl.Max(lom1x1, him1x1);
            uint1x1 smallestm1x1 = Hlsl.Min(lom1x1, him1x1);
            this.results[12] = new uint2(clampedm1x1.M11, this.highest);
            this.results[13] = new uint2(biggestm1x1.M11, this.highest);
            this.results[14] = new uint2(smallestm1x1.M11, this.lowest);

            uint1x2 outsidem1x2 = new(this.outside, this.outside);
            uint1x2 lom1x2 = new(this.lowest, this.lowest);
            uint1x2 him1x2 = new(this.highest, this.highest);
            uint1x2 clampedm1x2 = Hlsl.Clamp(outsidem1x2, lom1x2, him1x2);
            uint1x2 biggestm1x2 = Hlsl.Max(lom1x2, him1x2);
            uint1x2 smallestm1x2 = Hlsl.Min(lom1x2, him1x2);
            this.results[15] = new uint2(clampedm1x2.M12, this.highest);
            this.results[16] = new uint2(biggestm1x2.M12, this.highest);
            this.results[17] = new uint2(smallestm1x2.M12, this.lowest);

            uint1x3 outsidem1x3 = new(this.outside, this.outside, this.outside);
            uint1x3 lom1x3 = new(this.lowest, this.lowest, this.lowest);
            uint1x3 him1x3 = new(this.highest, this.highest, this.highest);
            uint1x3 clampedm1x3 = Hlsl.Clamp(outsidem1x3, lom1x3, him1x3);
            uint1x3 biggestm1x3 = Hlsl.Max(lom1x3, him1x3);
            uint1x3 smallestm1x3 = Hlsl.Min(lom1x3, him1x3);
            this.results[18] = new uint2(clampedm1x3.M13, this.highest);
            this.results[19] = new uint2(biggestm1x3.M13, this.highest);
            this.results[20] = new uint2(smallestm1x3.M13, this.lowest);

            uint1x4 outsidem1x4 = new(this.outside, this.outside, this.outside, this.outside);
            uint1x4 lom1x4 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            uint1x4 him1x4 = new(this.highest, this.highest, this.highest, this.highest);
            uint1x4 clampedm1x4 = Hlsl.Clamp(outsidem1x4, lom1x4, him1x4);
            uint1x4 biggestm1x4 = Hlsl.Max(lom1x4, him1x4);
            uint1x4 smallestm1x4 = Hlsl.Min(lom1x4, him1x4);
            this.results[21] = new uint2(clampedm1x4.M14, this.highest);
            this.results[22] = new uint2(biggestm1x4.M14, this.highest);
            this.results[23] = new uint2(smallestm1x4.M14, this.lowest);

            uint2x1 outsidem2x1 = new(this.outside, this.outside);
            uint2x1 lom2x1 = new(this.lowest, this.lowest);
            uint2x1 him2x1 = new(this.highest, this.highest);
            uint2x1 clampedm2x1 = Hlsl.Clamp(outsidem2x1, lom2x1, him2x1);
            uint2x1 biggestm2x1 = Hlsl.Max(lom2x1, him2x1);
            uint2x1 smallestm2x1 = Hlsl.Min(lom2x1, him2x1);
            this.results[24] = new uint2(clampedm2x1.M21, this.highest);
            this.results[25] = new uint2(biggestm2x1.M21, this.highest);
            this.results[26] = new uint2(smallestm2x1.M21, this.lowest);

            uint2x2 outsidem2x2 = new(this.outside, this.outside, this.outside, this.outside);
            uint2x2 lom2x2 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            uint2x2 him2x2 = new(this.highest, this.highest, this.highest, this.highest);
            uint2x2 clampedm2x2 = Hlsl.Clamp(outsidem2x2, lom2x2, him2x2);
            uint2x2 biggestm2x2 = Hlsl.Max(lom2x2, him2x2);
            uint2x2 smallestm2x2 = Hlsl.Min(lom2x2, him2x2);
            this.results[27] = new uint2(clampedm2x2.M22, this.highest);
            this.results[28] = new uint2(biggestm2x2.M22, this.highest);
            this.results[29] = new uint2(smallestm2x2.M22, this.lowest);

            uint2x3 outsidem2x3 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint2x3 lom2x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint2x3 him2x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint2x3 clampedm2x3 = Hlsl.Clamp(outsidem2x3, lom2x3, him2x3);
            uint2x3 biggestm2x3 = Hlsl.Max(lom2x3, him2x3);
            uint2x3 smallestm2x3 = Hlsl.Min(lom2x3, him2x3);
            this.results[30] = new uint2(clampedm2x3.M23, this.highest);
            this.results[31] = new uint2(biggestm2x3.M23, this.highest);
            this.results[32] = new uint2(smallestm2x3.M23, this.lowest);

            uint2x4 outsidem2x4 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint2x4 lom2x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint2x4 him2x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint2x4 clampedm2x4 = Hlsl.Clamp(outsidem2x4, lom2x4, him2x4);
            uint2x4 biggestm2x4 = Hlsl.Max(lom2x4, him2x4);
            uint2x4 smallestm2x4 = Hlsl.Min(lom2x4, him2x4);
            this.results[33] = new uint2(clampedm2x4.M24, this.highest);
            this.results[34] = new uint2(biggestm2x4.M24, this.highest);
            this.results[35] = new uint2(smallestm2x4.M24, this.lowest);

            uint3x1 outsidem3x1 = new(this.outside, this.outside, this.outside);
            uint3x1 lom3x1 = new(this.lowest, this.lowest, this.lowest);
            uint3x1 him3x1 = new(this.highest, this.highest, this.highest);
            uint3x1 clampedm3x1 = Hlsl.Clamp(outsidem3x1, lom3x1, him3x1);
            uint3x1 biggestm3x1 = Hlsl.Max(lom3x1, him3x1);
            uint3x1 smallestm3x1 = Hlsl.Min(lom3x1, him3x1);
            this.results[36] = new uint2(clampedm3x1.M31, this.highest);
            this.results[37] = new uint2(biggestm3x1.M31, this.highest);
            this.results[38] = new uint2(smallestm3x1.M31, this.lowest);

            uint3x2 outsidem3x2 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint3x2 lom3x2 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint3x2 him3x2 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint3x2 clampedm3x2 = Hlsl.Clamp(outsidem3x2, lom3x2, him3x2);
            uint3x2 biggestm3x2 = Hlsl.Max(lom3x2, him3x2);
            uint3x2 smallestm3x2 = Hlsl.Min(lom3x2, him3x2);
            this.results[39] = new uint2(clampedm3x2.M32, this.highest);
            this.results[40] = new uint2(biggestm3x2.M32, this.highest);
            this.results[41] = new uint2(smallestm3x2.M32, this.lowest);

            uint3x3 outsidem3x3 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint3x3 lom3x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint3x3 him3x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint3x3 clampedm3x3 = Hlsl.Clamp(outsidem3x3, lom3x3, him3x3);
            uint3x3 biggestm3x3 = Hlsl.Max(lom3x3, him3x3);
            uint3x3 smallestm3x3 = Hlsl.Min(lom3x3, him3x3);
            this.results[42] = new uint2(clampedm3x3.M33, this.highest);
            this.results[43] = new uint2(biggestm3x3.M33, this.highest);
            this.results[44] = new uint2(smallestm3x3.M33, this.lowest);

            uint3x4 outsidem3x4 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint3x4 lom3x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint3x4 him3x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint3x4 clampedm3x4 = Hlsl.Clamp(outsidem3x4, lom3x4, him3x4);
            uint3x4 biggestm3x4 = Hlsl.Max(lom3x4, him3x4);
            uint3x4 smallestm3x4 = Hlsl.Min(lom3x4, him3x4);
            this.results[45] = new uint2(clampedm3x4.M34, this.highest);
            this.results[46] = new uint2(biggestm3x4.M34, this.highest);
            this.results[47] = new uint2(smallestm3x4.M34, this.lowest);

            uint4x1 outsidem4x1 = new(this.outside, this.outside, this.outside, this.outside);
            uint4x1 lom4x1 = new(this.lowest, this.lowest, this.lowest, this.lowest);
            uint4x1 him4x1 = new(this.highest, this.highest, this.highest, this.highest);
            uint4x1 clampedm4x1 = Hlsl.Clamp(outsidem4x1, lom4x1, him4x1);
            uint4x1 biggestm4x1 = Hlsl.Max(lom4x1, him4x1);
            uint4x1 smallestm4x1 = Hlsl.Min(lom4x1, him4x1);
            this.results[48] = new uint2(clampedm4x1.M41, this.highest);
            this.results[49] = new uint2(biggestm4x1.M41, this.highest);
            this.results[50] = new uint2(smallestm4x1.M41, this.lowest);

            uint4x2 outsidem4x2 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint4x2 lom4x2 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint4x2 him4x2 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint4x2 clampedm4x2 = Hlsl.Clamp(outsidem4x2, lom4x2, him4x2);
            uint4x2 biggestm4x2 = Hlsl.Max(lom4x2, him4x2);
            uint4x2 smallestm4x2 = Hlsl.Min(lom4x2, him4x2);
            this.results[51] = new uint2(clampedm4x2.M42, this.highest);
            this.results[52] = new uint2(biggestm4x2.M42, this.highest);
            this.results[53] = new uint2(smallestm4x2.M42, this.lowest);

            uint4x3 outsidem4x3 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint4x3 lom4x3 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint4x3 him4x3 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint4x3 clampedm4x3 = Hlsl.Clamp(outsidem4x3, lom4x3, him4x3);
            uint4x3 biggestm4x3 = Hlsl.Max(lom4x3, him4x3);
            uint4x3 smallestm4x3 = Hlsl.Min(lom4x3, him4x3);
            this.results[54] = new uint2(clampedm4x3.M43, this.highest);
            this.results[55] = new uint2(biggestm4x3.M43, this.highest);
            this.results[56] = new uint2(smallestm4x3.M43, this.lowest);

            uint4x4 outsidem4x4 = new(this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside, this.outside);
            uint4x4 lom4x4 = new(this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest, this.lowest);
            uint4x4 him4x4 = new(this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest, this.highest);
            uint4x4 clampedm4x4 = Hlsl.Clamp(outsidem4x4, lom4x4, him4x4);
            uint4x4 biggestm4x4 = Hlsl.Max(lom4x4, him4x4);
            uint4x4 smallestm4x4 = Hlsl.Min(lom4x4, him4x4);
            this.results[57] = new uint2(clampedm4x4.M44, this.highest);
            this.results[58] = new uint2(biggestm4x4.M44, this.highest);
            this.results[59] = new uint2(smallestm4x4.M44, this.lowest);
        }
    }
}
