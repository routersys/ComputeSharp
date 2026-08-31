using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests;

/// <summary>
/// Tests that a default value reaches HLSL as the zero of its type.
/// </summary>
/// <remarks>
/// <para>
/// HLSL has no <see langword="default"/>, so the rewriter writes both the explicit form and the bare
/// literal as a cast of zero to the target type. The two forms are written by different overrides, so
/// they are exercised separately here.
/// </para>
/// <para>
/// A shader reaching this path compiles either way, because any number cast to the target type is
/// valid HLSL. The value is what tells a correct rewrite from an incorrect one.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void DefaultExpressions(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(8);

        device.Get().For(1, new DefaultExpressionShader(buffer));

        float[] results = buffer.ToArray();

        Assert.AreEqual(0, results[0], "an explicit default of a scalar type");
        Assert.AreEqual(0, results[1], "an explicit default of an integer type");
        Assert.AreEqual(0, results[2], "an explicit default of a vector type");
        Assert.AreEqual(0, results[3], "an explicit default of a matrix type");
        Assert.AreEqual(0, results[4], "a bare default literal");
        Assert.AreEqual(0, results[5], "a bare default literal of a vector type");
    }

    [AutoConstructor]
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct DefaultExpressionShader : IComputeShader
    {
        public readonly ReadWriteBuffer<float> buffer;

        public void Execute()
        {
            float explicitScalar = default(float);
            int explicitInteger = default(int);
            float3 explicitVector = default(float3);
            float2x2 explicitMatrix = default(float2x2);
            float bareScalar = default;
            float2 bareVector = default;

            this.buffer[0] = explicitScalar;
            this.buffer[1] = explicitInteger;
            this.buffer[2] = explicitVector.X + explicitVector.Y + explicitVector.Z;
            this.buffer[3] = explicitMatrix.M11 + explicitMatrix.M12 + explicitMatrix.M21 + explicitMatrix.M22;
            this.buffer[4] = bareScalar;
            this.buffer[5] = bareVector.X + bareVector.Y;
        }
    }
}
