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

    // A two by three matrix is used because the intrinsic has no overload for a square one. Every element is
    // distinct, so each slot pins one position of the result against the position it must have come from
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
}
