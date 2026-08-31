using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Which shaders declare that every thread group has to fall entirely inside the requested range.
/// </summary>
/// <remarks>
/// The generated entry point runs the body only for the threads inside the range, so a shader that waits for
/// its whole thread group cannot be run over a range that leaves part of a group out. The declaration is what
/// carries that to the dispatch, and only the shaders that wait for their group are meant to carry it.
/// </remarks>
[TestClass]
public class FullThreadGroupsTests
{
    [TestMethod]
    [DataRow("Hlsl.AllMemoryBarrierWithGroupSync();", "ShaderAllMemoryBarrierWithGroupSyncTests")]
    [DataRow("Hlsl.DeviceMemoryBarrierWithGroupSync();", "ShaderDeviceMemoryBarrierWithGroupSyncTests")]
    [DataRow("Hlsl.GroupMemoryBarrierWithGroupSync();", "ShaderGroupMemoryBarrierWithGroupSyncTests")]
    public void AShaderThatWaitsForItsGroupDeclaresTheRequirement(string body, string assemblyName)
    {
        Assert.IsTrue(IsDeclared(body, assemblyName), Generated(body, assemblyName));
    }

    [TestMethod]
    [DataRow("Hlsl.AllMemoryBarrier();", "ShaderAllMemoryBarrierTests")]
    [DataRow("Hlsl.DeviceMemoryBarrier();", "ShaderDeviceMemoryBarrierTests")]
    [DataRow("Hlsl.GroupMemoryBarrier();", "ShaderGroupMemoryBarrierTests")]
    [DataRow("k += 1;", "ShaderNoBarrierTests")]
    public void AShaderThatDoesNotWaitLeavesTheRequirementOut(string body, string assemblyName)
    {
        Assert.IsFalse(IsDeclared(body, assemblyName), Generated(body, assemblyName));
    }

    /// <summary>
    /// A barrier reached through a method the shader calls, rather than written in the entry point.
    /// </summary>
    /// <remarks>
    /// The rewriter walks one method at a time, so the flag has to survive being raised on a method that is
    /// not the entry point. Without this row, tracking only the entry point would pass every row above.
    /// </remarks>
    [TestMethod]
    public void ABarrierInsideACalledMethodDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static void Wait()
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Helper.Wait();

                    this.buffer[ThreadIds.X] = 1;
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInACalledMethodTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// Runs the generator over a shader carrying a body and gets its generated descriptor.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The generated descriptor for the shader.</returns>
    private static string Generated(string body, string assemblyName)
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
                    int k = 1;

                    {{body}}

                    this.buffer[ThreadIds.X] = k;
                }
            }
            """;

        return GenerateDescriptor(source, assemblyName);
    }

    /// <summary>
    /// Checks whether the generated descriptor declares the requirement.
    /// </summary>
    /// <param name="body">The statements to put in the shader body.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>Whether the descriptor declares <c>RequiresFullThreadGroups</c>.</returns>
    private static bool IsDeclared(string body, string assemblyName)
    {
        return Generated(body, assemblyName).Contains("RequiresFullThreadGroups");
    }

    /// <summary>
    /// Runs the generator over a source and gets the descriptor it produced.
    /// </summary>
    /// <param name="source">The source of the shader to run the generator over.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The generated descriptor.</returns>
    private static string GenerateDescriptor(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([source], assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), "Shaders.Shader");
    }
}
