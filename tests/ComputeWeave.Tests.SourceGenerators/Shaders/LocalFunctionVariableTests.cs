using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Where the declaration of a variable written in an argument position ends up.
/// </summary>
/// <remarks>
/// HLSL has no declaration expression, so a variable declared in an argument is written as a declaration of
/// its own and the argument is left naming it. Which body that declaration is written into is what these
/// pin: a local function is written out as a function of its own, so a declaration placed in the body that
/// held the local function leaves the function reading an identifier nothing declares.
/// </remarks>
[TestClass]
public class LocalFunctionVariableTests
{
    /// <summary>
    /// A variable declared in an argument of a call inside a local function.
    /// </summary>
    [TestMethod]
    [DataRow("out float whole", "float whole", "LocalFunctionOutVariableTests")]
    [DataRow("out _", "float __implicit0", "LocalFunctionDiscardTests")]
    public void AVariableDeclaredInsideALocalFunctionIsWrittenThere(string argument, string identifier, string assemblyName)
    {
        string source = $$"""
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    static float Split(float value)
                    {
                        return Hlsl.Modf(value, {{argument}});
                    }

                    this.buffer[0] = Split(2.5f);
                }
            }
            """;

        AssertDeclaredIn(source, assemblyName, identifier, "__Execute__Split");
    }

    /// <summary>
    /// The same call written in a method of the shader, which is the shape the declaration already reaches
    /// the right body in. Without it, a change moving every declaration out of every body would pass.
    /// </summary>
    [TestMethod]
    public void AVariableDeclaredInsideAMethodIsWrittenThere()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private static float Split(float value)
                {
                    return Hlsl.Modf(value, out float whole) + whole;
                }

                public void Execute()
                {
                    this.buffer[0] = Split(2.5f);
                }
            }
            """;

        AssertDeclaredIn(Source, "MethodOutVariableTests", "float whole", "Split");
    }

    /// <summary>
    /// A variable declared before a local function, in the body that holds it. The declaration belongs to
    /// the enclosing body and has to survive the local function being rewritten between it and the write.
    /// </summary>
    /// <remarks>
    /// Written before the local function rather than after it. A declaration raised afterwards goes into
    /// whichever list is current by then, which is the enclosing one whether or not it was ever put back,
    /// so a row writing it after the function says nothing about the list being restored.
    /// </remarks>
    [TestMethod]
    public void AVariableDeclaredBeforeALocalFunctionIsWrittenInTheEnclosingBody()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    float fraction = Hlsl.Modf(2.5f, out float whole);

                    static float Twice(float value)
                    {
                        return value * 2;
                    }

                    this.buffer[0] = Twice(fraction) + whole;
                }
            }
            """;

        AssertDeclaredIn(Source, "BeforeLocalFunctionOutVariableTests", "float whole", "Execute");
    }

    /// <summary>
    /// A local function that declares nothing. The body of the enclosing method has to keep holding the
    /// declarations written in it, which a change emptying every enclosing body would break.
    /// </summary>
    [TestMethod]
    public void AVariableDeclaredBesideALocalFunctionIsWrittenInTheEnclosingBody()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    static float Twice(float value)
                    {
                        return value * 2;
                    }

                    this.buffer[0] = Twice(Hlsl.Modf(2.5f, out float whole)) + whole;
                }
            }
            """;

        AssertDeclaredIn(Source, "EnclosingBodyOutVariableTests", "float whole", "Execute");
    }

    /// <summary>
    /// Asserts that the declaration of an identifier is written into the body of one function.
    /// </summary>
    /// <param name="source">The shader source to generate from.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <param name="identifier">The identifier whose declaration is looked for.</param>
    /// <param name="function">The name of the function the declaration has to sit in.</param>
    /// <remarks>
    /// The generated source is split on the declaration of each function, so what is asserted is which of
    /// them holds the declaration rather than that the text contains it somewhere.
    /// </remarks>
    private static void AssertDeclaredIn(string source, string assemblyName, string identifier, string function)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        string generated = GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader");
        string[] lines = generated.Split('\n');
        string declaration = $"{identifier};";
        string[] body = BodyOf(lines, function);

        Assert.AreNotEqual(0, body.Length, $"'{function}' has no body:\n{generated}");

        Assert.IsTrue(
            body.Any(line => line.Trim() == declaration),
            $"'{declaration}' is not written inside '{function}':\n{generated}");

        // One declaration and no more: a variable written into two bodies sits in the one asked for and in
        // another beside it, which the check above answers yes to
        Assert.AreEqual(
            1,
            lines.Count(line => line.Trim() == declaration),
            $"'{declaration}' is written more than once:\n{generated}");
    }

    /// <summary>
    /// Takes the lines of the body of one function out of a generated source.
    /// </summary>
    /// <param name="lines">The lines of the generated source.</param>
    /// <param name="function">The name of the function to take the body of.</param>
    /// <returns>The lines between the brace opening the body and the one closing it.</returns>
    /// <remarks>
    /// The HLSL sits inside a raw string literal of the generated C#, so every line of it carries the
    /// indentation of that literal. The body is delimited by the brace at the indentation of the signature
    /// rather than by one at the start of a line, which inside the literal never occurs.
    /// </remarks>
    private static string[] BodyOf(string[] lines, string function)
    {
        for (int index = 0; index < lines.Length; index++)
        {
            // A forward declaration carries the same name and ends in a semicolon, so the body is the one
            // the opening brace follows rather than the first line the name appears on
            if (!lines[index].Contains($"{function}(") ||
                index + 1 == lines.Length ||
                lines[index + 1].Trim() != "{")
            {
                continue;
            }

            string closing = new(' ', lines[index].Length - lines[index].TrimStart().Length);

            for (int end = index + 2; end < lines.Length; end++)
            {
                if (lines[end].TrimEnd() == closing + "}")
                {
                    return lines[(index + 2)..end];
                }
            }
        }

        return [];
    }
}
