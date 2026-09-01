using System.Linq;
using ComputeWeave.SourceGeneration.Constants;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// What survives an edit that changes where a shader sits without changing what it says.
/// </summary>
/// <remarks>
/// The bytecode is compiled in a node of its own so that every shader in a compilation can be compiled at
/// once, and the info needed to report a compile diagnostic is captured during the transform, a symbol not
/// being usable past that point. That captured info carries the location of the shader type, so an edit
/// anywhere above the type moves it and the transform produces a different model. The node that joins the
/// bytecode back in drops the captured info once it has served its purpose, and these tests are what pins
/// that: without it the moved location reaches the output model and every shader is written out again.
/// </remarks>
[TestClass]
public class ShaderGeneratorIncrementalityTests
{
    private const string ShaderSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 1;
            }
        }
        """;

    /// <summary>
    /// An edit above the shader type, which moves it without changing a single thing about it.
    /// </summary>
    private const string MovedShaderSource = """
        // An edit above the shader, which moves every declaration below it
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 1;
            }
        }
        """;

    [TestMethod]
    public void AShaderThatOnlyMovedIsNotWrittenOutAgain()
    {
        GeneratorRunResult result = RunTwice(ShaderSource, MovedShaderSource, out string clean, out string incremental);

        // The generated source is the same text either way, which is what the reader of the output sees
        Assert.AreEqual(clean, incremental);

        // The transform has to have noticed the edit, or the assertion below would hold for a shader the
        // second run never looked at, and would keep holding if the join node stopped dropping anything
        Assert.AreEqual(
            IncrementalStepRunReason.Modified,
            result.TrackedSteps[WellKnownTrackingNames.Execute].Single().Outputs[0].Reason,
            "the edit did not reach the transform, so this test is not measuring the join node");

        // Having noticed it, the pipeline has to arrive at the same model, so nothing is written out again
        Assert.IsTrue(
            result.TrackedSteps[WellKnownTrackingNames.Output]
                .SelectMany(static step => step.Outputs)
                .All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
            string.Join(
                ", ",
                result.TrackedSteps[WellKnownTrackingNames.Output]
                    .SelectMany(static step => step.Outputs)
                    .Select(static output => output.Reason)));
    }

    /// <summary>
    /// The control. An edit that changes what the shader says is written out again.
    /// </summary>
    [TestMethod]
    public void AShaderThatChangedIsWrittenOutAgain()
    {
        const string EditedShaderSource = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = 2;
                }
            }
            """;

        GeneratorRunResult result = RunTwice(ShaderSource, EditedShaderSource, out string clean, out string incremental);

        Assert.AreNotEqual(clean, incremental);

        Assert.AreEqual(
            IncrementalStepRunReason.Modified,
            result.TrackedSteps[WellKnownTrackingNames.Output].Single().Outputs[0].Reason);
    }

    /// <summary>
    /// Runs the generator over a source, replaces that source, and runs it again on the same driver.
    /// </summary>
    /// <param name="source">The source to run over first.</param>
    /// <param name="updatedSource">The source to replace it with before the second run.</param>
    /// <param name="clean">The source generated by the first run.</param>
    /// <param name="incremental">The source generated by the second run.</param>
    /// <returns>The result of the second run.</returns>
    private static GeneratorRunResult RunTwice(string source, string updatedSource, out string clean, out string incremental)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([source], "ShaderGeneratorIncrementalityTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator(), trackIncrementalGeneratorSteps: true);

        clean = GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out GeneratorDriver cleanDriver), "Shaders.Shader");

        CSharpCompilation updatedCompilation = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(),
            CompilationHelper.ParseTree(updatedSource));

        incremental = GeneratorHelper.GetGeneratedSource(
            GeneratorHelper.Run(cleanDriver, updatedCompilation, out GeneratorDriver incrementalDriver),
            "Shaders.Shader");

        return incrementalDriver.GetRunResult().Results[0];
    }
}
