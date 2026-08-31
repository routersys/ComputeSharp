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

    // A two by three matrix pins the non-square shape. Every element is distinct, so each slot pins one
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

    // A square shape is the case the intrinsic had no overload for. The off-diagonal elements differ, so a
    // call that returned its argument unchanged would disagree in every slot below. Three by three is here
    // because a two by two swap alone would not catch a transpose confined to the leading block, and four by
    // four pins the largest shape. One by one is different in kind: its transpose is itself, so no value can
    // separate a real transpose from one that returns its argument. That slot pins only that the shape is
    // accepted and runs, which is worth holding because a single element matrix is the one most likely to
    // be collapsed into a scalar
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

    // A boolean matrix had no square overload either, and like the signed one it had never been run. The two
    // values come from a field rather than from literals, so the pattern cannot be folded away before the
    // transpose runs. Reading both sides of a slot through the same conversion compares positions but says
    // nothing about the conversion, so the last two slots pin the conversion itself against literals: without
    // them, a conversion that collapsed to a single value would let every other slot agree with itself
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
}
