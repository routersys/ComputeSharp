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
    /// A barrier inside an instance method of a custom type the shader calls.
    /// </summary>
    [TestMethod]
    public void ABarrierInsideAnImportedInstanceMethodDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public float Wait()
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();

                    return Amount;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Helper helper = default;

                    this.buffer[ThreadIds.X] = helper.Wait();
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInAnImportedInstanceMethodTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// A barrier inside a constructor the shader calls.
    /// </summary>
    [TestMethod]
    public void ABarrierInsideAnImportedConstructorDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public Helper(float amount)
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();

                    Amount = amount;
                }

                public static float Read(Helper helper) => helper.Amount;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helper.Read(new Helper(2.0f));
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInAnImportedConstructorTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// A barrier inside the initializer of a static field of an external type the shader reads.
    /// </summary>
    /// <remarks>
    /// Such a field is rewritten by the rewriter for initializers, which the shader body creates on reading it,
    /// so this row answers for the path from that rewriter back out to the body that reached it.
    /// </remarks>
    [TestMethod]
    public void ABarrierInsideAnExternalStaticFieldInitializerDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Waiter
            {
                public static float Wait()
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();

                    return 1;
                }
            }

            internal static class Helper
            {
                public static readonly float Value = Waiter.Wait();
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helper.Value;
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInAnExternalStaticFieldInitializerTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// A barrier inside a method imported by a static field initializer of the shader itself.
    /// </summary>
    /// <remarks>
    /// The initializer is rewritten before the descriptor is written, and the declarations it imports are
    /// rewritten by a rewriter it creates, so this row answers for the path out of that one.
    /// </remarks>
    [TestMethod]
    public void ABarrierInsideAMethodImportedByAnInitializerDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static float Wait()
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();

                    return 1;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Helper.Wait();

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInAMethodImportedByAnInitializerTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// A barrier inside a constructor imported by a static field initializer of the shader itself.
    /// </summary>
    [TestMethod]
    public void ABarrierInsideAConstructorImportedByAnInitializerDeclaresTheRequirement()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public Helper(float amount)
                {
                    Hlsl.GroupMemoryBarrierWithGroupSync();

                    Amount = amount;
                }

                public static float Read(Helper helper) => helper.Amount;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Helper.Read(new Helper(2.0f));

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderBarrierInAConstructorImportedByAnInitializerTests");

        Assert.IsTrue(generated.Contains("RequiresFullThreadGroups"), generated);
    }

    /// <summary>
    /// The same shapes without a barrier in them. The requirement is raised by the call and not by the path
    /// that reached it, so a path answering for its own sake would pass every row above on its own.
    /// </summary>
    [TestMethod]
    public void ADeclarationWithoutABarrierLeavesTheRequirementOut()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public Helper(float amount)
                {
                    Hlsl.GroupMemoryBarrier();

                    Amount = amount;
                }

                public static float Read(Helper helper) => helper.Amount;
            }

            internal static class Outer
            {
                public static readonly float Value = Helper.Read(new Helper(2.0f));
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Helper.Read(new Helper(3.0f));

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale + Outer.Value;
                }
            }
            """;

        string generated = GenerateDescriptor(Source, "ShaderNoBarrierInAnyDeclarationTests");

        Assert.IsFalse(generated.Contains("RequiresFullThreadGroups"), generated);
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
