using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <inheritdoc/>
partial class HlslIntrinsicSemanticsTests
{
    /// <summary>
    /// Asserts that every slot a probe wrote holds two equal values.
    /// </summary>
    /// <param name="results">The buffer the probe wrote.</param>
    private static void AssertAgreesExactly(ReadWriteBuffer<uint2> results)
    {
        uint2[] pairs = results.ToArray();

        for (int i = 0; i < pairs.Length; i++)
        {
            Assert.AreEqual(pairs[i].Y, pairs[i].X, $"slot {i}");
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AsUInt(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(1);

        device.Get().For(1, new AsUIntShader(results, 1.0f, 0x3F800000u));

        AssertAgreesExactly(results);
    }

    // reinterpreting 1.0f keeps its bits, which IEEE 754 fixes at 0x3F800000
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsUIntShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly float value;
        public readonly uint expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.AsUInt(this.value), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_AsFloat(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new AsFloatShader(results, 0x3F800000u, 1.0f));

        AssertAgrees(results, 0.0f);
    }

    // the reverse reinterpretation of the same bits
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AsFloatShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly uint bits;
        public readonly float expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.AsFloat(this.bits), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_CountBits(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(1);

        device.Get().For(1, new CountBitsShader(results, 0xF0F0F0F0u, 16u));

        AssertAgreesExactly(results);
    }

    // four nibbles of ones set sixteen bits
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct CountBitsShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint value;
        public readonly uint expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.CountBits(this.value), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_ReverseBits(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(1);

        device.Get().For(1, new ReverseBitsShader(results, 0x0000000Fu, 0xF0000000u));

        AssertAgreesExactly(results);
    }

    // the four lowest bits become the four highest
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ReverseBitsShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint value;
        public readonly uint expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.ReverseBits(this.value), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_FirstBitHighAndLow(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(3);

        device.Get().For(1, new FirstBitShader(results, 0x00001010u, 0x00000010u, 0x00001000u));

        AssertAgreesExactly(results);
    }

    // The two intrinsics search from opposite ends. Which index they report is neither documented here nor
    // settled across compiler versions, so the slots compare results against each other instead of against a
    // number: the low search must ignore the higher bit, the high search must ignore the lower one, and with a
    // single bit set the two must agree. Swapping the two mappings breaks one of the first two slots
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct FirstBitShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint both;
        public readonly uint low;
        public readonly uint high;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.FirstBitLow(this.both), Hlsl.FirstBitLow(this.low));
            this.results[1] = new uint2(Hlsl.FirstBitHigh(this.both), Hlsl.FirstBitHigh(this.high));
            this.results[2] = new uint2(Hlsl.FirstBitHigh(this.low), Hlsl.FirstBitLow(this.low));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Float32ToFloat16(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(1);

        device.Get().For(1, new Float32ToFloat16Shader(results, 1.0f, 0x3C00u));

        AssertAgreesExactly(results);
    }

    // half precision writes 1.0 as 0x3C00
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Float32ToFloat16Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly float value;
        public readonly uint expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(Hlsl.Float32ToFloat16(this.value), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Float16ToFloat32(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(1);

        device.Get().For(1, new Float16ToFloat32Shader(results, 0x3C00u, 1.0f));

        AssertAgrees(results, 0.0f);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Float16ToFloat32Shader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly uint bits;
        public readonly float expected;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Float16ToFloat32(this.bits), this.expected);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_DotUnsigned(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(3);

        device.Get().For(1, new DotUnsignedShader(results, 2u, 3u, 5u, 7u));

        AssertAgreesExactly(results);
    }

    // the lanes are paired so that reversing either operand changes the sum, which a symmetric pairing would hide
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DotUnsignedShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;
        public readonly uint a;
        public readonly uint b;
        public readonly uint c;
        public readonly uint d;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new uint2(
                Hlsl.Dot(new UInt2(this.a, this.b), new UInt2(this.c, this.d)),
                (this.a * this.c) + (this.b * this.d));

            this.results[1] = new uint2(
                Hlsl.Dot(new UInt3(this.a, this.b, this.c), new UInt3(this.b, this.c, this.d)),
                (this.a * this.b) + (this.b * this.c) + (this.c * this.d));

            this.results[2] = new uint2(
                Hlsl.Dot(new UInt4(this.a, this.b, this.c, this.d), new UInt4(this.b, this.c, this.d, this.a)),
                (this.a * this.b) + (this.b * this.c) + (this.c * this.d) + (this.d * this.a));
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_All(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new AllShader(results, new float2(1.0f, 0.0f), new float2(1.0f, 1.0f)));

        AssertAgrees(results, 0.0f);
    }

    // a mixed vector separates all from any: all rejects it, any accepts it
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AllShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float2 mixed;
        public readonly float2 ones;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.All(this.mixed) ? 1.0f : 0.0f, 0.0f);
            this.results[1] = new float2(Hlsl.All(this.ones) ? 1.0f : 0.0f, 1.0f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Any(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new AnyShader(results, new float2(1.0f, 0.0f), new float2(0.0f, 0.0f)));

        AssertAgrees(results, 0.0f);
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct AnyShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float2 mixed;
        public readonly float2 zeros;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.Any(this.mixed) ? 1.0f : 0.0f, 1.0f);
            this.results[1] = new float2(Hlsl.Any(this.zeros) ? 1.0f : 0.0f, 0.0f);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_IsFinite(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(2);

        device.Get().For(1, new IsFiniteShader(results, 1.25f, float.PositiveInfinity));

        AssertAgrees(results, 0.0f);
    }

    // the infinity arrives through the constant buffer, so the compiler cannot fold the answer
    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct IsFiniteShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;
        public readonly float finite;
        public readonly float infinite;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = new float2(Hlsl.IsFinite(this.finite) ? 1.0f : 0.0f, 1.0f);
            this.results[1] = new float2(Hlsl.IsFinite(this.infinite) ? 1.0f : 0.0f, 0.0f);
        }
    }
}
