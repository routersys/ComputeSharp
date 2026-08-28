using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The discovered type needs an explicit constructor, as the generator rejects primary constructors on one
#pragma warning disable IDE0290

namespace ComputeWeave.Tests;

/// <summary>
/// Tests that a parameter default value reaches HLSL where HLSL accepts one, and means the same thing.
/// </summary>
/// <remarks>
/// <para>
/// HLSL takes default values from the first prototype of a function. The generator writes a forward
/// declaration for every method and the implementation after it, so the default belongs on the
/// declaration alone. Writing it on both is a redefinition, and writing it only on the implementation
/// leaves the calls that appear before the implementation without one.
/// </para>
/// <para>
/// Compiling is not enough: an argument the call site omits has to arrive with the value C# would have
/// bound. One shader covers every path that writes a method into HLSL, and each of them is given a
/// different default so that a value arriving from the wrong one is visible.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void ParameterDefaults(Device device)
    {
        using ReadWriteBuffer<float> buffer = device.Get().AllocateReadWriteBuffer<float>(8);

        device.Get().For(1, new ParameterDefaultShader(buffer));

        float[] results = buffer.ToArray();

        Assert.AreEqual(2, results[0], "the external static method did not use its default");
        Assert.AreEqual(3, results[1], "the method on the shader type did not use its default");
        Assert.AreEqual(6, results[2], "the local function did not use its default");
        Assert.AreEqual(4, results[3], "the instance method on the discovered type did not use its default");
        Assert.AreEqual(6, results[4], "the constructor of the discovered type did not use its default");
    }

    /// <summary>
    /// A helper whose static method carries a default value.
    /// </summary>
    internal static class ParameterDefaultHelper
    {
        public static float Scale(float value, float factor = 2.0f)
        {
            return value * factor;
        }
    }

    /// <summary>
    /// A discovered type whose constructor and instance method both carry a default value.
    /// </summary>
    internal struct ParameterDefaultValueType
    {
        public float Amount;

        public ParameterDefaultValueType(float amount, float extra = 5.0f)
        {
            this.Amount = amount + extra;
        }

        public readonly float Scaled(float value, float factor = 4.0f)
        {
            return value * factor;
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    internal readonly partial struct ParameterDefaultShader(ReadWriteBuffer<float> buffer) : IComputeShader
    {
        public void Execute()
        {
            static float Local(float input, float factor = 6.0f)
            {
                return input * factor;
            }

            ParameterDefaultValueType value = default;
            ParameterDefaultValueType constructed = new(1.0f);

            buffer[0] = ParameterDefaultHelper.Scale(1.0f);
            buffer[1] = Member(1.0f);
            buffer[2] = Local(1.0f);
            buffer[3] = value.Scaled(1.0f);
            buffer[4] = constructed.Amount;
        }

        private static float Member(float value, float factor = 3.0f)
        {
            return value * factor;
        }
    }
}
