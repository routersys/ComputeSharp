using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <summary>
/// Tests pinning the meaning of the <see cref="Hlsl"/> intrinsics, not just their compilability.
/// </summary>
/// <remarks>
/// Each shader writes one <c>float2(actual, expected)</c> per slot, where the expected side is either an
/// identity built from operators and intrinsics that shader tests already exercise, or a value the reader
/// can check by hand. A mapping that reaches the wrong HLSL intrinsic, or that reorders arguments, moves
/// the two sides apart. The tolerances are sized to catch that, not to certify precision: implementations
/// are free to differ in the last bits, so agreement is asserted well above the noise.
/// </remarks>
[TestClass]
public partial class HlslIntrinsicSemanticsTests
{
    /// <summary>
    /// Asserts that every slot a probe wrote holds two agreeing values.
    /// </summary>
    /// <param name="results">The buffer the probe wrote.</param>
    /// <param name="tolerance">The allowed absolute difference between the two sides.</param>
    private static void AssertAgrees(ReadWriteBuffer<float2> results, float tolerance)
    {
        float2[] pairs = results.ToArray();

        for (int i = 0; i < pairs.Length; i++)
        {
            Assert.AreEqual(pairs[i].Y, pairs[i].X, tolerance, $"slot {i}");
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Acos(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new AcosShader(results, 0.375f));

        AssertAgrees(results, 0.0001f);
    }

    // cos(acos(x)) is x, while cos(asin(x)) is not, so the identity separates the two inverse functions
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AcosShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Cos(Hlsl.Acos(this.x)), this.x);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Asin(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new AsinShader(results, 0.375f));

        AssertAgrees(results, 0.0001f);
    }

    // sin(asin(x)) is x, while sin(acos(x)) is not
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsinShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Sin(Hlsl.Asin(this.x)), this.x);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Atan2(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new Atan2Shader(results, 3.0f, 4.0f));

        AssertAgrees(results, 0.00001f);
    }

    // atan2(y, x) is atan(y / x) for a positive x; swapping the arguments gives atan(x / y) instead
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Atan2Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float y;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Atan2(this.y, this.x), Hlsl.Atan(this.y / this.x));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Ceil(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new CeilShader(results, 2.5f, -2.5f));

        AssertAgrees(results, 0.0f);
    }

    // ceil(x) is -floor(-x); both signs are probed because floor and ceil agree on neither
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct CeilShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float positive;
        public readonly float negative;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Ceil(this.positive), -Hlsl.Floor(-this.positive));
            this.results[1] = new float2(Hlsl.Ceil(this.negative), -Hlsl.Floor(-this.negative));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Cosh(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new CoshShader(results, 1.25f));

        AssertAgrees(results, 0.0001f);
    }

    // cosh(x) is the half sum of exp(x) and exp(-x); sinh is the half difference, so the two cannot be confused
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct CoshShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Cosh(this.x), (Hlsl.Exp(this.x) + Hlsl.Exp(-this.x)) * 0.5f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Degrees(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new DegreesShader(results, 1.25f));

        AssertAgrees(results, 0.001f);
    }

    // the factor is 180 over pi, written out so the expectation does not lean on the radians intrinsic
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DegreesShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Degrees(this.x), this.x * 57.29578f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Exp2(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new Exp2Shader(results, 3.5f));

        AssertAgrees(results, 0.001f);
    }

    // exp2(x) raises two to x, which pow states directly
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Exp2Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Exp2(this.x), Hlsl.Pow(2.0f, this.x));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Fmod(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new FmodShader(results, 7.5f, 2.0f, 1.5f, 2.0f));

        AssertAgrees(results, 0.0001f);
    }

    // fmod(7.5, 2) is 1.5 and fmod(2, 7.5) is 2, so probing both orders pins which argument is the divisor.
    // The remainder goes through a division, so it is not exact: this device returns 1.9999999 for the second
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FmodShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;
        public readonly float y;
        public readonly float expectedForward;
        public readonly float expectedReversed;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Fmod(this.x, this.y), this.expectedForward);
            this.results[1] = new float2(Hlsl.Fmod(this.y, this.x), this.expectedReversed);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Ldexp(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new LdexpShader(results, 3.0f, 4.0f));

        AssertAgrees(results, 0.001f);
    }

    // ldexp(x, exponent) scales x by a power of two; the arguments differ so a swap gives 32 instead of 48
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct LdexpShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;
        public readonly float exponent;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Ldexp(this.x, this.exponent), this.x * Hlsl.Pow(2.0f, this.exponent));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Log2(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new Log2Shader(results, 40.0f));

        AssertAgrees(results, 0.0001f);
    }

    // log2 and log10 differ only in their base, so each is checked against the natural logarithm
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Log2Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Log2(this.x), Hlsl.Log(this.x) / Hlsl.Log(2.0f));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Log10(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new Log10Shader(results, 1000.0f));

        AssertAgrees(results, 0.0001f);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Log10Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Log10(this.x), Hlsl.Log(this.x) / Hlsl.Log(10.0f));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Rcp(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new RcpShader(results, 3.0f));

        AssertAgrees(results, 0.00001f);
    }

    // rcp is an approximate reciprocal, so the divisor is not a power of two and the tolerance leaves room
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct RcpShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Rcp(this.x), 1.0f / this.x);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Round(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new RoundShader(results, 2.6f, -2.6f, 3.0f, -3.0f));

        AssertAgrees(results, 0.0f);
    }

    // rounding 2.6 and -2.6 gives 3 and -3, which floor, ceil and trunc each fail to produce together.
    // The inputs avoid a tie so the result does not depend on how halfway cases are broken
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct RoundShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float positive;
        public readonly float negative;
        public readonly float expectedPositive;
        public readonly float expectedNegative;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Round(this.positive), this.expectedPositive);
            this.results[1] = new float2(Hlsl.Round(this.negative), this.expectedNegative);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Rsqrt(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new RsqrtShader(results, 10.0f));

        AssertAgrees(results, 0.0001f);
    }

    // rsqrt is an approximate inverse square root, so the tolerance is looser than for rcp
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct RsqrtShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Rsqrt(this.x), 1.0f / Hlsl.Sqrt(this.x));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Sign(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(3);

        device.Get().For(1, new SignShader(results, 2.25f, -3.5f, 0.0f));

        AssertAgrees(results, 0.0f);
    }

    // sign returns an integer, so the expectation is built from comparisons rather than from another intrinsic
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SignShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float positive;
        public readonly float negative;
        public readonly float zero;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Sign(this.positive), this.positive > 0.0f ? 1.0f : (this.positive < 0.0f ? -1.0f : 0.0f));
            this.results[1] = new float2(Hlsl.Sign(this.negative), this.negative > 0.0f ? 1.0f : (this.negative < 0.0f ? -1.0f : 0.0f));
            this.results[2] = new float2(Hlsl.Sign(this.zero), this.zero > 0.0f ? 1.0f : (this.zero < 0.0f ? -1.0f : 0.0f));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Sinh(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new SinhShader(results, 1.25f));

        AssertAgrees(results, 0.0001f);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct SinhShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float x;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Sinh(this.x), (Hlsl.Exp(this.x) - Hlsl.Exp(-this.x)) * 0.5f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Trunc(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new TruncShader(results, 2.75f, -2.75f));

        AssertAgrees(results, 0.0f);
    }

    // trunc rounds toward zero, so it agrees with floor above zero and with -floor(-x) below it
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct TruncShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float positive;
        public readonly float negative;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Trunc(this.positive), Hlsl.Floor(this.positive));
            this.results[1] = new float2(Hlsl.Trunc(this.negative), -Hlsl.Floor(-this.negative));
        }
    }
}
