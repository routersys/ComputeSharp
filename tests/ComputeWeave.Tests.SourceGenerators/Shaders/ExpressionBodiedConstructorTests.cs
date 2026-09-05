using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// How a declaration written with an expression body is imported. The two forms mean the same thing in C#,
/// so a shader that computes a value has to compute it whichever form the author used to write it.
/// </summary>
/// <remarks>
/// The shaders here carry a thread group size, which is what turns shader compilation on, so a generated
/// source that HLSL rejects reaches these tests as a diagnostic rather than as a passing string match.
/// </remarks>
[TestClass]
public class ExpressionBodiedConstructorTests
{
    /// <summary>
    /// The reproduction from the issue, with the constructor reached from a static field initializer.
    /// </summary>
    private const string InitializerSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper(float amount) => Amount = amount;

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
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// The same shader with the constructor written as a block, which is the form that already worked.
    /// </summary>
    private const string InitializerBlockBodySource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper(float amount)
            {
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
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// The same constructor reached from the shader body. Both paths run the same import, but the body has
    /// been able to reach it for longer than the initializer has, so each one is pinned on its own.
    /// </summary>
    private const string BodySource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper(float amount) => Amount = amount;

            public static float Read(Helper helper) => helper.Amount;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Helper.Read(new Helper(2.0f));
            }
        }
        """;

    /// <summary>
    /// The same shader with the constructor written as a block.
    /// </summary>
    private const string BodyBlockBodySource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper(float amount)
            {
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
                this.buffer[0] = Helper.Read(new Helper(2.0f));
            }
        }
        """;

    /// <summary>
    /// An expression bodied method holding an implicit variable, which is the one shape that reads the body
    /// of a method rather than leaving it alone. It is the closest a method gets to what a constructor does.
    /// </summary>
    private const string ImplicitVariableMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            private static float Fraction(float value) => Hlsl.Modf(value, out float whole) + whole;

            public void Execute()
            {
                this.buffer[0] = Fraction(2.5f);
            }
        }
        """;

    /// <summary>
    /// An expression bodied constructor reached from a static field initializer is imported. The generator
    /// used to end with an exception here, which drops the whole output and reports unrelated errors.
    /// </summary>
    [TestMethod]
    public void AnExpressionBodiedConstructorInAStaticFieldInitializerIsImported()
    {
        string generated = Generate(InitializerSource, "ExpressionBodiedConstructorInitializerTests");

        Assert.IsTrue(generated.Contains("Shaders_Helper_Read(Shaders_Helper::__ctor(2.0))"), $"the call is not rewritten into the stub:\n{generated}");
        Assert.IsTrue(generated.Contains("static Shaders_Helper Shaders_Helper::__ctor(float amount)"), $"the stub is not written:\n{generated}");
    }

    /// <summary>
    /// The same constructor reached from the shader body.
    /// </summary>
    [TestMethod]
    public void AnExpressionBodiedConstructorInTheShaderBodyIsImported()
    {
        string generated = Generate(BodySource, "ExpressionBodiedConstructorBodyTests");

        Assert.IsTrue(generated.Contains("Shaders_Helper_Read(Shaders_Helper::__ctor(2.0))"), $"the call is not rewritten into the stub:\n{generated}");
        Assert.IsTrue(generated.Contains("static Shaders_Helper Shaders_Helper::__ctor(float amount)"), $"the stub is not written:\n{generated}");
    }

    /// <summary>
    /// The two forms produce the same shader. Reading the stub alone would accept a body that dropped the
    /// assignment the author wrote, which is a shader that compiles and computes a different number.
    /// </summary>
    [TestMethod]
    public void AnExpressionBodiedConstructorInAStaticFieldInitializerIsWrittenLikeABlockBodiedOne()
    {
        string expressionBodied = Generate(InitializerSource, "ExpressionBodiedConstructorParityTests");
        string blockBodied = Generate(InitializerBlockBodySource, "ExpressionBodiedConstructorParityTests");

        Assert.AreEqual(blockBodied, expressionBodied);
    }

    /// <summary>
    /// The same parity for the shader body path.
    /// </summary>
    [TestMethod]
    public void AnExpressionBodiedConstructorInTheShaderBodyIsWrittenLikeABlockBodiedOne()
    {
        string expressionBodied = Generate(BodySource, "ExpressionBodiedConstructorBodyParityTests");
        string blockBodied = Generate(BodyBlockBodySource, "ExpressionBodiedConstructorBodyParityTests");

        Assert.AreEqual(blockBodied, expressionBodied);
    }

    /// <summary>
    /// The method path is accepted with an implicit variable in an expression body. The issue read this as a
    /// second way to reach the same exception, so it is measured rather than left as a reading.
    /// </summary>
    [TestMethod]
    public void AnExpressionBodiedMethodHoldingAnImplicitVariableIsAccepted()
    {
        string generated = Generate(ImplicitVariableMethodSource, "ExpressionBodiedMethodImplicitVariableTests");

        Assert.IsTrue(generated.Contains("modf(value, whole)"), $"the intrinsic is not written:\n{generated}");
    }

    private static string Generate(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        ImmutableArray<Diagnostic> diagnostics = result.Diagnostics;

        Assert.IsTrue(
            diagnostics.IsEmpty,
            string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader");
    }
}
