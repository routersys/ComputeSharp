using System.Linq;
using System.Text.RegularExpressions;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class ExtensionMethodTests
{
    /// <summary>
    /// One shader reaching every shape of receiver an extension method can take: a value, a custom type, an
    /// <see langword="in"/> receiver, a <see langword="ref"/> receiver that writes to it, an HLSL primitive,
    /// a chained call, a call with further arguments, and the same method called as a plain static method.
    /// </summary>
    private const string Source = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Value
        {
            public float Amount;
        }

        internal static class Ext
        {
            public static float Doubled(this float value) => value * 2;

            public static float Scaled(this float value, float factor) => value * factor;

            public static float Get(this Value value) => value.Amount;

            public static float GetIn(this in Value value) => value.Amount;

            public static void Fill(this ref Value value) => value.Amount = 3;

            public static float Sum(this Float4 value) => value.X + value.Y;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                Value value = default;
                Value mutated = default;
                Float4 vector = new(1, 2, 3, 4);

                mutated.Fill();

                this.buffer[0] = 2.0f.Doubled();
                this.buffer[1] = value.Get();
                this.buffer[2] = value.GetIn();
                this.buffer[3] = vector.Sum();
                this.buffer[4] = 2.0f.Doubled().Doubled();
                this.buffer[5] = 2.0f.Scaled(3.0f);
                this.buffer[6] = Ext.Doubled(4.0f);
                this.buffer[7] = mutated.Amount;
            }
        }
        """;

    /// <summary>
    /// The calls whose receiver is a literal, so that the expected text does not depend on how a local is
    /// renamed. The chained one pins that a receiver which is itself a rewritten call is carried through.
    /// </summary>
    private static readonly string[] Invocations =
    [
        "Shaders_Ext_Doubled(2.0)",
        "Shaders_Ext_Scaled(2.0, 3.0)",
        "Shaders_Ext_Doubled(Shaders_Ext_Doubled(2.0))",
        "Shaders_Ext_Doubled(4.0)"
    ];

    [TestMethod]
    public void ExtensionMethodReceiversReachTheArgumentList()
    {
        string generated = Generate();

        foreach (string invocation in Invocations)
        {
            Assert.IsTrue(generated.Contains(invocation), $"'{invocation}' does not reach the generated HLSL:\n{generated}");
        }
    }

    /// <summary>
    /// The family the shapes above stand for. An imported extension method declares its receiver as the first
    /// parameter, so no call of one can have an empty argument list, whatever the receiver is written as.
    /// </summary>
    [TestMethod]
    public void NoExtensionMethodIsCalledWithoutItsReceiver()
    {
        string generated = Generate();

        MatchCollection matches = Regex.Matches(generated, @"Shaders_Ext_[A-Za-z]+\(\)");

        Assert.AreEqual(0, matches.Count, $"{matches.Count} call(s) lost the receiver:\n{generated}");
    }

    /// <summary>
    /// The declarations the calls above have to agree with, which is what fails when a receiver is lost.
    /// </summary>
    [TestMethod]
    public void EveryImportedExtensionMethodDeclaresItsReceiver()
    {
        string generated = Generate();

        string[] declarations = [.. generated.Split('\n').Where(static line => line.TrimStart().StartsWith("static ") && line.Contains("Shaders_Ext_") && line.TrimEnd().EndsWith(");"))];

        Assert.AreEqual(6, declarations.Length, $"the six extension methods are not all declared:\n{generated}");

        foreach (string declaration in declarations)
        {
            Assert.IsFalse(declaration.Contains("()"), $"a declaration lost its receiver:\n{declaration}");
        }
    }

    private static string Generate()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [Source],
            "ShaderExtensionMethodTests",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Shaders.Shader");
    }
}
