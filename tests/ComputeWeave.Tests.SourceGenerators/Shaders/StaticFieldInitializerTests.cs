using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// What a static field initializer may call. A different rewriter handles those initializers from the one
/// that handles the shader body, so the two can disagree about the same call.
/// </summary>
/// <remarks>
/// The shaders here carry a thread group size, which is what turns shader compilation on, so a generated
/// source that HLSL rejects reaches these tests as a diagnostic rather than as a passing string match.
/// </remarks>
[TestClass]
public class StaticFieldInitializerTests
{
    private const string IntrinsicSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Hlsl.Abs(-2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    private const string ShaderMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Member(2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            private static float Member(float value) => value * 2;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// The reproduction from the issue this import was added for.
    /// </summary>
    private const string ExternalMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static float Twice(float value) => value * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Helper.Twice(2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// An imported method that declares a local function. HLSL has no nested functions, so the rewriter
    /// lifts them to top level, and an initializer has to carry them out the same way a body does.
    /// </summary>
    private const string LocalFunctionSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static float Twice(float value)
            {
                static float Inner(float inner) => inner * 2;

                return Inner(value);
            }
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Helper.Twice(2.0f);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// A user defined constructor. The shader body imports one, so an initializer answering with a
    /// default value instead computes a different number from the expression the author wrote.
    /// </summary>
    private const string ConstructorSource = """
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
    /// The parameterless constructor a struct always has. It sets no field, so a default value is
    /// what it computes in C# too, and the import is not the answer for it.
    /// </summary>
    private const string ImplicitConstructorSource = """
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
            private static readonly float Scale = Helper.Read(new Helper());

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// An imported constructor that declares a local function. HLSL has no nested functions, so the
    /// ones an import lifts out are carried over to the initializer the same way a method's are.
    /// </summary>
    private const string ConstructorLocalFunctionSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper(float amount)
            {
                static float Inner(float inner) => inner * 2;

                Amount = Inner(amount);
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

    [TestMethod]
    public void AnIntrinsicIsWrittenUnderItsHlslName()
    {
        string generated = Generate(IntrinsicSource, "StaticFieldIntrinsicTests");

        Assert.IsTrue(generated.Contains("abs(-2.0)"), generated);
    }

    /// <summary>
    /// A call to a function the generator wrote is accepted in a static field initializer, because the
    /// forward declarations are written ahead of the static fields. This is what makes it possible to
    /// import a method into an initializer at all, so it is pinned rather than left to be re-derived.
    /// </summary>
    [TestMethod]
    public void AMethodOfTheShaderTypeIsCalled()
    {
        string generated = Generate(ShaderMethodSource, "StaticFieldShaderMethodTests");

        Assert.IsTrue(generated.Contains("Member(2.0)"), generated);
    }

    /// <summary>
    /// An external static method is imported, the same as it is from the shader body, and the call is
    /// renamed to the imported declaration rather than left naming a type the HLSL compiler never saw.
    /// </summary>
    [TestMethod]
    public void AnExternalStaticMethodIsImported()
    {
        string generated = Generate(ExternalMethodSource, "StaticFieldExternalMethodTests");

        Assert.IsFalse(generated.Contains("Helper.Twice"), $"the call is written out as it stands:\n{generated}");
        Assert.IsTrue(generated.Contains("Shaders_Helper_Twice(2.0)"), $"the call is not renamed to the import:\n{generated}");
        Assert.IsTrue(generated.Contains("float Shaders_Helper_Twice(float value)"), $"the declaration is not imported:\n{generated}");
    }

    /// <summary>
    /// The local functions of an imported method are written too. Without that, the initializer would call
    /// a function the generated HLSL never declares, which the shader compiler reports as a diagnostic.
    /// </summary>
    [TestMethod]
    public void ALocalFunctionOfAnImportedMethodIsWritten()
    {
        string generated = Generate(LocalFunctionSource, "StaticFieldLocalFunctionTests");

        Assert.IsTrue(generated.Contains("Shaders_Helper_Twice(2.0)"), $"the call is not renamed to the import:\n{generated}");
        Assert.IsTrue(generated.Contains("Inner"), $"the local function is not written:\n{generated}");
    }

    /// <summary>
    /// A user defined constructor is imported, the same as it is from the shader body, so that the
    /// same expression computes the same value wherever the author writes it.
    /// </summary>
    [TestMethod]
    public void AUserDefinedConstructorIsImported()
    {
        string generated = Generate(ConstructorSource, "StaticFieldConstructorTests");

        Assert.IsFalse(generated.Contains("Shaders_Helper_Read((Shaders_Helper)0)"), $"the constructor is collapsed into a default value:\n{generated}");
        Assert.IsTrue(generated.Contains("Shaders_Helper_Read(Shaders_Helper::__ctor(2.0))"), $"the call is not rewritten into the stub:\n{generated}");
        Assert.IsTrue(generated.Contains("static Shaders_Helper Shaders_Helper::__ctor(float amount)"), $"the stub is not written:\n{generated}");
    }

    /// <summary>
    /// The parameterless constructor stays a default value. Importing it would write a stub around a
    /// constructor that has no declaration to import, and the value it computes is the same either way.
    /// </summary>
    [TestMethod]
    public void AnImplicitParameterlessConstructorStaysADefaultValue()
    {
        string generated = Generate(ImplicitConstructorSource, "StaticFieldImplicitConstructorTests");

        Assert.IsTrue(generated.Contains("Shaders_Helper_Read((Shaders_Helper)0)"), $"the default value is not written:\n{generated}");
        Assert.IsFalse(generated.Contains("__ctor"), $"a stub is written for a constructor with no declaration:\n{generated}");
    }

    /// <summary>
    /// The local functions of an imported constructor are written too. Without that, the initializer
    /// calls a function the generated HLSL never declares, which the shader compiler reports.
    /// </summary>
    [TestMethod]
    public void ALocalFunctionOfAnImportedConstructorIsWritten()
    {
        string generated = Generate(ConstructorLocalFunctionSource, "StaticFieldConstructorLocalFunctionTests");

        Assert.IsTrue(generated.Contains("Shaders_Helper::__ctor(2.0)"), $"the call is not rewritten into the stub:\n{generated}");
        Assert.IsTrue(generated.Contains("Inner"), $"the local function is not written:\n{generated}");
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
