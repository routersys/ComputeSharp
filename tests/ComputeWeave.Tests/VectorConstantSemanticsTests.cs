using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0649, CS8618

namespace ComputeWeave.Tests;

/// <summary>
/// Tests pinning the vector constants a shader body may use.
/// </summary>
/// <remarks>
/// Every one of these constants is written twice: once as a value in the runtime, and once as an HLSL literal
/// in the generator's mapping table. The two are separate artifacts, and a transposed component in either one
/// compiles and computes a different value. Each shader here reads the constant on the device, and the test
/// compares it against the runtime value of the same name, so the two spellings are checked against each other
/// rather than against a number written a third time.
/// </remarks>
[TestClass]
public partial class VectorConstantSemanticsTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Bool2Constants(Device device)
    {
        using ReadWriteBuffer<int2> results = device.Get().AllocateReadWriteBuffer<int2>(4);

        device.Get().For(1, new Bool2ConstantShader(results));

        int2[] values = results.ToArray();

        AssertBool2(bool2.False, values[0], "False");
        AssertBool2(bool2.True, values[1], "True");
        AssertBool2(bool2.TrueX, values[2], "TrueX");
        AssertBool2(bool2.TrueY, values[3], "TrueY");
    }

    // a boolean vector cannot be read back directly, so the shader converts it to an integer one first
    private static void AssertBool2(bool2 expected, int2 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X != 0, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y != 0, $"{name}.Y");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Bool2ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int2> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.BoolToInt(bool2.False);
            this.results[1] = Hlsl.BoolToInt(bool2.True);
            this.results[2] = Hlsl.BoolToInt(bool2.TrueX);
            this.results[3] = Hlsl.BoolToInt(bool2.TrueY);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Bool3Constants(Device device)
    {
        using ReadWriteBuffer<int3> results = device.Get().AllocateReadWriteBuffer<int3>(5);

        device.Get().For(1, new Bool3ConstantShader(results));

        int3[] values = results.ToArray();

        AssertBool3(bool3.False, values[0], "False");
        AssertBool3(bool3.True, values[1], "True");
        AssertBool3(bool3.TrueX, values[2], "TrueX");
        AssertBool3(bool3.TrueY, values[3], "TrueY");
        AssertBool3(bool3.TrueZ, values[4], "TrueZ");
    }

    private static void AssertBool3(bool3 expected, int3 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X != 0, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y != 0, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z != 0, $"{name}.Z");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Bool3ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int3> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.BoolToInt(bool3.False);
            this.results[1] = Hlsl.BoolToInt(bool3.True);
            this.results[2] = Hlsl.BoolToInt(bool3.TrueX);
            this.results[3] = Hlsl.BoolToInt(bool3.TrueY);
            this.results[4] = Hlsl.BoolToInt(bool3.TrueZ);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Bool4Constants(Device device)
    {
        using ReadWriteBuffer<int4> results = device.Get().AllocateReadWriteBuffer<int4>(6);

        device.Get().For(1, new Bool4ConstantShader(results));

        int4[] values = results.ToArray();

        AssertBool4(bool4.False, values[0], "False");
        AssertBool4(bool4.True, values[1], "True");
        AssertBool4(bool4.TrueX, values[2], "TrueX");
        AssertBool4(bool4.TrueY, values[3], "TrueY");
        AssertBool4(bool4.TrueZ, values[4], "TrueZ");
        AssertBool4(bool4.TrueW, values[5], "TrueW");
    }

    private static void AssertBool4(bool4 expected, int4 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X != 0, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y != 0, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z != 0, $"{name}.Z");
        Assert.AreEqual(expected.W, actual.W != 0, $"{name}.W");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Bool4ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int4> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = Hlsl.BoolToInt(bool4.False);
            this.results[1] = Hlsl.BoolToInt(bool4.True);
            this.results[2] = Hlsl.BoolToInt(bool4.TrueX);
            this.results[3] = Hlsl.BoolToInt(bool4.TrueY);
            this.results[4] = Hlsl.BoolToInt(bool4.TrueZ);
            this.results[5] = Hlsl.BoolToInt(bool4.TrueW);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Int2Constants(Device device)
    {
        using ReadWriteBuffer<int2> results = device.Get().AllocateReadWriteBuffer<int2>(4);

        device.Get().For(1, new Int2ConstantShader(results));

        int2[] values = results.ToArray();

        AssertInt2(int2.Zero, values[0], "Zero");
        AssertInt2(int2.One, values[1], "One");
        AssertInt2(int2.UnitX, values[2], "UnitX");
        AssertInt2(int2.UnitY, values[3], "UnitY");
    }

    private static void AssertInt2(int2 expected, int2 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Int2ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int2> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = int2.Zero;
            this.results[1] = int2.One;
            this.results[2] = int2.UnitX;
            this.results[3] = int2.UnitY;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Int3Constants(Device device)
    {
        using ReadWriteBuffer<int3> results = device.Get().AllocateReadWriteBuffer<int3>(5);

        device.Get().For(1, new Int3ConstantShader(results));

        int3[] values = results.ToArray();

        AssertInt3(int3.Zero, values[0], "Zero");
        AssertInt3(int3.One, values[1], "One");
        AssertInt3(int3.UnitX, values[2], "UnitX");
        AssertInt3(int3.UnitY, values[3], "UnitY");
        AssertInt3(int3.UnitZ, values[4], "UnitZ");
    }

    private static void AssertInt3(int3 expected, int3 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Int3ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int3> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = int3.Zero;
            this.results[1] = int3.One;
            this.results[2] = int3.UnitX;
            this.results[3] = int3.UnitY;
            this.results[4] = int3.UnitZ;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Int4Constants(Device device)
    {
        using ReadWriteBuffer<int4> results = device.Get().AllocateReadWriteBuffer<int4>(6);

        device.Get().For(1, new Int4ConstantShader(results));

        int4[] values = results.ToArray();

        AssertInt4(int4.Zero, values[0], "Zero");
        AssertInt4(int4.One, values[1], "One");
        AssertInt4(int4.UnitX, values[2], "UnitX");
        AssertInt4(int4.UnitY, values[3], "UnitY");
        AssertInt4(int4.UnitZ, values[4], "UnitZ");
        AssertInt4(int4.UnitW, values[5], "UnitW");
    }

    private static void AssertInt4(int4 expected, int4 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
        Assert.AreEqual(expected.W, actual.W, $"{name}.W");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Int4ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<int4> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = int4.Zero;
            this.results[1] = int4.One;
            this.results[2] = int4.UnitX;
            this.results[3] = int4.UnitY;
            this.results[4] = int4.UnitZ;
            this.results[5] = int4.UnitW;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_UInt2Constants(Device device)
    {
        using ReadWriteBuffer<uint2> results = device.Get().AllocateReadWriteBuffer<uint2>(4);

        device.Get().For(1, new UInt2ConstantShader(results));

        uint2[] values = results.ToArray();

        AssertUInt2(uint2.Zero, values[0], "Zero");
        AssertUInt2(uint2.One, values[1], "One");
        AssertUInt2(uint2.UnitX, values[2], "UnitX");
        AssertUInt2(uint2.UnitY, values[3], "UnitY");
    }

    private static void AssertUInt2(uint2 expected, uint2 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct UInt2ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint2> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = uint2.Zero;
            this.results[1] = uint2.One;
            this.results[2] = uint2.UnitX;
            this.results[3] = uint2.UnitY;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_UInt3Constants(Device device)
    {
        using ReadWriteBuffer<uint3> results = device.Get().AllocateReadWriteBuffer<uint3>(5);

        device.Get().For(1, new UInt3ConstantShader(results));

        uint3[] values = results.ToArray();

        AssertUInt3(uint3.Zero, values[0], "Zero");
        AssertUInt3(uint3.One, values[1], "One");
        AssertUInt3(uint3.UnitX, values[2], "UnitX");
        AssertUInt3(uint3.UnitY, values[3], "UnitY");
        AssertUInt3(uint3.UnitZ, values[4], "UnitZ");
    }

    private static void AssertUInt3(uint3 expected, uint3 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct UInt3ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint3> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = uint3.Zero;
            this.results[1] = uint3.One;
            this.results[2] = uint3.UnitX;
            this.results[3] = uint3.UnitY;
            this.results[4] = uint3.UnitZ;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_UInt4Constants(Device device)
    {
        using ReadWriteBuffer<uint4> results = device.Get().AllocateReadWriteBuffer<uint4>(6);

        device.Get().For(1, new UInt4ConstantShader(results));

        uint4[] values = results.ToArray();

        AssertUInt4(uint4.Zero, values[0], "Zero");
        AssertUInt4(uint4.One, values[1], "One");
        AssertUInt4(uint4.UnitX, values[2], "UnitX");
        AssertUInt4(uint4.UnitY, values[3], "UnitY");
        AssertUInt4(uint4.UnitZ, values[4], "UnitZ");
        AssertUInt4(uint4.UnitW, values[5], "UnitW");
    }

    private static void AssertUInt4(uint4 expected, uint4 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
        Assert.AreEqual(expected.W, actual.W, $"{name}.W");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct UInt4ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<uint4> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = uint4.Zero;
            this.results[1] = uint4.One;
            this.results[2] = uint4.UnitX;
            this.results[3] = uint4.UnitY;
            this.results[4] = uint4.UnitZ;
            this.results[5] = uint4.UnitW;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Float2Constants(Device device)
    {
        using ReadWriteBuffer<float2> results = device.Get().AllocateReadWriteBuffer<float2>(4);

        device.Get().For(1, new Float2ConstantShader(results));

        float2[] values = results.ToArray();

        AssertFloat2(float2.Zero, values[0], "Zero");
        AssertFloat2(float2.One, values[1], "One");
        AssertFloat2(float2.UnitX, values[2], "UnitX");
        AssertFloat2(float2.UnitY, values[3], "UnitY");
    }

    // the constants are exactly representable, so nothing here needs a tolerance
    private static void AssertFloat2(float2 expected, float2 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Float2ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float2> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = float2.Zero;
            this.results[1] = float2.One;
            this.results[2] = float2.UnitX;
            this.results[3] = float2.UnitY;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Float3Constants(Device device)
    {
        using ReadWriteBuffer<float3> results = device.Get().AllocateReadWriteBuffer<float3>(5);

        device.Get().For(1, new Float3ConstantShader(results));

        float3[] values = results.ToArray();

        AssertFloat3(float3.Zero, values[0], "Zero");
        AssertFloat3(float3.One, values[1], "One");
        AssertFloat3(float3.UnitX, values[2], "UnitX");
        AssertFloat3(float3.UnitY, values[3], "UnitY");
        AssertFloat3(float3.UnitZ, values[4], "UnitZ");
    }

    private static void AssertFloat3(float3 expected, float3 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Float3ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float3> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = float3.Zero;
            this.results[1] = float3.One;
            this.results[2] = float3.UnitX;
            this.results[3] = float3.UnitY;
            this.results[4] = float3.UnitZ;
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void Verify_Float4Constants(Device device)
    {
        using ReadWriteBuffer<float4> results = device.Get().AllocateReadWriteBuffer<float4>(6);

        device.Get().For(1, new Float4ConstantShader(results));

        float4[] values = results.ToArray();

        AssertFloat4(float4.Zero, values[0], "Zero");
        AssertFloat4(float4.One, values[1], "One");
        AssertFloat4(float4.UnitX, values[2], "UnitX");
        AssertFloat4(float4.UnitY, values[3], "UnitY");
        AssertFloat4(float4.UnitZ, values[4], "UnitZ");
        AssertFloat4(float4.UnitW, values[5], "UnitW");
    }

    private static void AssertFloat4(float4 expected, float4 actual, string name)
    {
        Assert.AreEqual(expected.X, actual.X, $"{name}.X");
        Assert.AreEqual(expected.Y, actual.Y, $"{name}.Y");
        Assert.AreEqual(expected.Z, actual.Z, $"{name}.Z");
        Assert.AreEqual(expected.W, actual.W, $"{name}.W");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct Float4ConstantShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float4> results;

        /// <inheritdoc/>
        public void Execute()
        {
            this.results[0] = float4.Zero;
            this.results[1] = float4.One;
            this.results[2] = float4.UnitX;
            this.results[3] = float4.UnitY;
            this.results[4] = float4.UnitZ;
            this.results[5] = float4.UnitW;
        }
    }
}
