using System.Collections.Generic;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class ParameterDefaultTests
{
    /// <summary>
    /// One shader reaching every path that writes a method into HLSL: an external static method, a method on
    /// the shader type, a local function, and an instance method and a constructor on a discovered type.
    /// </summary>
    private const string Source = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static float Scale(float value, float factor = 2.0f)
            {
                return value * factor;
            }
        }

        internal struct Value
        {
            public float Amount;

            public Value(float amount, float extra = 5.0f)
            {
                Amount = amount + extra;
            }

            public readonly float Scaled(float value, float factor = 4.0f)
            {
                return value * factor;
            }
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            private static float Member(float value, float factor = 3.0f)
            {
                return value * factor;
            }

            public void Execute()
            {
                Value value = default;
                Value constructed = new(1.0f);

                this.buffer[0] = Helper.Scale(1.0f);
                this.buffer[1] = Member(1.0f);
                this.buffer[2] = Local(1.0f);
                this.buffer[3] = value.Scaled(1.0f);
                this.buffer[4] = constructed.Amount;

                static float Local(float input, float factor = 6.0f)
                {
                    return input * factor;
                }
            }
        }
        """;

    /// <summary>
    /// The default value written by each of the paths above, chosen so that each one is unambiguous.
    /// </summary>
    private static readonly string[] DefaultValues = ["= 2.0", "= 3.0", "= 4.0", "= 5.0", "= 6.0"];

    [TestMethod]
    public void ParameterDefaultValuesAreWrittenOnForwardDeclarationsOnly()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [Source],
            "ShaderParameterDefaultTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        string generated = GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Shaders.Shader");

        foreach (string defaultValue in DefaultValues)
        {
            List<string> lines = [.. generated.Split('\n').Where(line => line.Contains(defaultValue))];

            // A default value that reaches no declaration at all would pass the check below vacuously
            Assert.AreNotEqual(0, lines.Count, $"'{defaultValue}' does not reach the generated HLSL:\n{generated}");

            // HLSL takes default values from the first prototype only, and the forward declaration is that
            // prototype, so every occurrence has to end a declaration rather than open an implementation
            foreach (string line in lines)
            {
                Assert.IsTrue(line.TrimEnd().EndsWith(");"), $"'{defaultValue}' is not on a forward declaration:\n{line}");
            }
        }
    }
}
