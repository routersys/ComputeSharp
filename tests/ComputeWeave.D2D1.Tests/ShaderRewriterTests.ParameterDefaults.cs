using System.Collections.Generic;
using System.Linq;
using ComputeWeave.D2D1.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The discovered type needs an explicit constructor, as the generator rejects primary constructors on one
#pragma warning disable IDE0290

namespace ComputeWeave.D2D1.Tests;

/// <summary>
/// Tests that a parameter default value reaches the Direct2D HLSL only where HLSL accepts one.
/// </summary>
/// <remarks>
/// <para>
/// HLSL takes default values from the first prototype of a function. The generator writes a forward
/// declaration for every method and the implementation after it, so the default belongs on the
/// declaration alone. Writing it on both is a redefinition, and writing it only on the implementation
/// leaves the calls that appear before the implementation without one.
/// </para>
/// <para>
/// The shader carries an explicit profile, so the generator compiles it with FXC while this project is
/// built. All three wrong forms fail that compilation, which makes the build a net for this on the
/// Direct2D side even though this suite is not part of any workflow.
/// </para>
/// </remarks>
public partial class ShaderRewriterTests
{
    /// <summary>
    /// The default value written by each path that reaches HLSL, chosen so that each one is unambiguous.
    /// </summary>
    private static readonly string[] ParameterDefaultValues = ["= 2.0", "= 3.0", "= 4.0", "= 5.0", "= 6.0"];

    [TestMethod]
    public void ParameterDefaults_AreWrittenOnForwardDeclarationsOnly()
    {
        string hlslSource = D2D1ReflectionServices.GetShaderInfo<ParameterDefaultShader>().HlslSource;

        foreach (string defaultValue in ParameterDefaultValues)
        {
            List<string> lines = [.. hlslSource.Split('\n').Where(line => line.Contains(defaultValue))];

            // A default value that reaches no declaration at all would pass the check below vacuously
            Assert.AreNotEqual(0, lines.Count, $"'{defaultValue}' does not reach the generated HLSL:\n{hlslSource}");

            foreach (string line in lines)
            {
                Assert.IsTrue(line.TrimEnd().EndsWith(");"), $"'{defaultValue}' is not on a forward declaration:\n{line}");
            }
        }
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

    /// <summary>
    /// A shader reaching every path that writes a method into HLSL: an external static method, a method
    /// on the shader type, a local function, and an instance method and a constructor on a discovered type.
    /// </summary>
    /// <param name="seed">The value to pass to each of them.</param>
    [D2DInputCount(0)]
    [D2DShaderProfile(D2D1ShaderProfile.PixelShader50)]
    [D2DGeneratedPixelShaderDescriptor]
    internal readonly partial struct ParameterDefaultShader(float seed) : ID2D1PixelShader
    {
        public float4 Execute()
        {
            static float Local(float input, float factor = 6.0f)
            {
                return input * factor;
            }

            ParameterDefaultValueType value = default;
            ParameterDefaultValueType constructed = new(1.0f);

            float total =
                ParameterDefaultHelper.Scale(seed) +
                Member(seed) +
                Local(seed) +
                value.Scaled(seed) +
                constructed.Amount;

            return total > 0 ? new float4(0, 1, 0, 1) : new float4(1, 0, 0, 1);
        }

        private static float Member(float value, float factor = 3.0f)
        {
            return value * factor;
        }
    }
}
