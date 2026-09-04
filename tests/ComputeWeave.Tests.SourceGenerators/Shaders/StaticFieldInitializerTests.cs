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

    /// <summary>
    /// A parameterless constructor the author declared. It has a body to import, which is what tells it
    /// apart from the one a struct always has, so it is imported like any other.
    /// </summary>
    private const string ExplicitParameterlessConstructorSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public Helper()
            {
                Amount = 7;
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
    /// A static field of an external type, read from an initializer.
    /// </summary>
    private const string ExternalFieldSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static readonly float Factor = 2.0f;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Helper.Factor;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// A static field of the shader type itself, read from an initializer through the type name.
    /// </summary>
    private const string OwnFieldSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Factor = 2.0f;

            private static readonly float Scale = Shader.Factor;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// An external static field whose own initializer reads another one by name alone, reached from the
    /// shader body. The import runs the initializer rewriter, so the body meets this too.
    /// </summary>
    private const string ChainedFieldSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static readonly float Baseline = 2.0f;

            public static readonly float Factor = Baseline * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Helper.Factor;
            }
        }
        """;

    /// <summary>
    /// A static field of an external type whose own initializer reads it back through the import.
    /// </summary>
    private const string CyclicFieldSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static readonly float Factor = Factor + 1;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Helper.Factor;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// A static field declared on the shader type, read from the initializer of an imported one.
    /// </summary>
    private const string ImportedReadingDeclaredSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static readonly float Doubled = Shader.Baseline * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            public static readonly float Baseline = 2.0f;

            private static readonly float Scale = Helper.Doubled;

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// An out argument written as a declaration, in an initializer.
    /// </summary>
    private const string OutVariableSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Hlsl.Modf(1.5f, out float whole);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// A discarded out argument, in an initializer.
    /// </summary>
    private const string DiscardedOutSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private static readonly float Scale = Hlsl.Modf(1.5f, out _);

            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Scale;
            }
        }
        """;

    /// <summary>
    /// The same two forms in the shader body, where the variable has a body to be declared in.
    /// </summary>
    private const string OutVariableInBodySource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Hlsl.Modf(1.5f, out float whole) + whole;
            }
        }
        """;

    /// <inheritdoc cref="OutVariableInBodySource"/>
    private const string DiscardedOutInBodySource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = Hlsl.Modf(1.5f, out _);
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

    /// <summary>
    /// A declared parameterless constructor is imported. Reading the argument count alone would collapse
    /// it along with the one a struct always has, and the body the author wrote would never run.
    /// </summary>
    [TestMethod]
    public void AnExplicitParameterlessConstructorIsImported()
    {
        string generated = Generate(ExplicitParameterlessConstructorSource, "StaticFieldExplicitParameterlessConstructorTests");

        Assert.IsFalse(generated.Contains("Shaders_Helper_Read((Shaders_Helper)0)"), $"the constructor is collapsed into a default value:\n{generated}");
        Assert.IsTrue(generated.Contains("Shaders_Helper_Read(Shaders_Helper::__ctor())"), $"the call is not rewritten into the stub:\n{generated}");
    }

    /// <summary>
    /// An external static field is imported, the same as it is from the shader body, rather than left
    /// naming a type the generated HLSL never declares.
    /// </summary>
    [TestMethod]
    public void AnExternalStaticFieldIsImported()
    {
        string generated = Generate(ExternalFieldSource, "StaticFieldExternalFieldTests");

        Assert.IsFalse(generated.Contains("Helper.Factor"), $"the read is written out as it stands:\n{generated}");
        Assert.IsTrue(generated.Contains("static const float Shaders_Helper_Factor = 2.0"), $"the field is not imported:\n{generated}");
        Assert.IsTrue(generated.Contains("static const float Scale = Shaders_Helper_Factor"), $"the read is not renamed to the import:\n{generated}");
    }

    /// <summary>
    /// A static field of the shader type is written under the name it has in the shader, the way the body
    /// writes it, rather than under the type name the author qualified it with.
    /// </summary>
    [TestMethod]
    public void AStaticFieldOfTheShaderTypeIsNamedDirectly()
    {
        string generated = Generate(OwnFieldSource, "StaticFieldOwnFieldTests");

        Assert.IsFalse(generated.Contains("Shader.Factor"), $"the type name is written out:\n{generated}");
        Assert.IsTrue(generated.Contains("static const float Scale = Factor"), $"the read is not written under the shader name:\n{generated}");
    }

    /// <summary>
    /// A static field read by name alone, from the initializer of an imported field. The shader body
    /// reaches this through the import, so this form is not one only an initializer can meet.
    /// </summary>
    [TestMethod]
    public void AnExternalStaticFieldReadByNameAloneIsImported()
    {
        string generated = Generate(ChainedFieldSource, "StaticFieldChainedFieldTests");

        Assert.IsTrue(generated.Contains("static const float Shaders_Helper_Baseline = 2.0"), $"the field read by name alone is not imported:\n{generated}");
        Assert.IsTrue(generated.Contains("Shaders_Helper_Factor = Shaders_Helper_Baseline * 2"), $"the read is not renamed to the import:\n{generated}");
    }

    /// <summary>
    /// A read that closes a cycle is answered by the report the import already carries, rather than by the
    /// fault that claiming the entry exists to stop.
    /// </summary>
    [TestMethod]
    public void AReadThatClosesACycleIsReported()
    {
        Assert.AreEqual("CMPW0124", Report(CyclicFieldSource, "StaticFieldCyclicFieldTests"));
    }

    /// <summary>
    /// A field declared on the shader type is written ahead of an imported field whose initializer reads
    /// it. The two are one sequence ordered by when each finished, so neither group is written as a block.
    /// </summary>
    [TestMethod]
    public void ADeclaredStaticFieldReadFromAnImportedInitializerIsWrittenFirst()
    {
        string generated = Generate(ImportedReadingDeclaredSource, "StaticFieldImportedReadingDeclaredTests");

        int declaration = generated.IndexOf("static const float Baseline = 2.0", System.StringComparison.Ordinal);
        int read = generated.IndexOf("Shaders_Helper_Doubled = Baseline * 2", System.StringComparison.Ordinal);

        Assert.AreNotEqual(-1, declaration, $"the declared field is not written:\n{generated}");
        Assert.AreNotEqual(-1, read, $"the imported field is not written:\n{generated}");
        Assert.IsTrue(declaration < read, $"the imported field is written ahead of the declaration it reads:\n{generated}");
    }

    /// <summary>
    /// An out argument written as a declaration is refused in an initializer.
    /// </summary>
    [TestMethod]
    public void AnOutVariableDeclaredInAnInitializerIsRefused()
    {
        Assert.AreEqual("CMPW0126", Report(OutVariableSource, "StaticFieldOutVariableTests"));
    }

    /// <summary>
    /// A discarded out argument is refused in an initializer, the variable it stands for being one the
    /// rewriting introduces rather than one the author wrote.
    /// </summary>
    [TestMethod]
    public void ADiscardedOutArgumentInAnInitializerIsRefused()
    {
        Assert.AreEqual("CMPW0126", Report(DiscardedOutSource, "StaticFieldDiscardedOutTests"));
    }

    /// <summary>
    /// The same declaration in the shader body is not refused, and the variable is declared ahead of the
    /// call. A refusal reaching the body would leave every row above green.
    /// </summary>
    [TestMethod]
    public void AnOutVariableDeclaredInTheShaderBodyIsNotRefused()
    {
        string generated = Generate(OutVariableInBodySource, "ShaderBodyOutVariableTests");

        Assert.IsTrue(generated.Contains("float whole;"), $"the variable is not declared ahead of the call:\n{generated}");
    }

    /// <inheritdoc cref="AnOutVariableDeclaredInTheShaderBodyIsNotRefused"/>
    [TestMethod]
    public void ADiscardedOutArgumentInTheShaderBodyIsNotRefused()
    {
        string generated = Generate(DiscardedOutInBodySource, "ShaderBodyDiscardedOutTests");

        Assert.IsTrue(generated.Contains("__implicit0"), $"the variable is not declared ahead of the call:\n{generated}");
    }

    private static string Generate(string source, string assemblyName)
    {
        GeneratorRunResult result = Run(source, assemblyName);

        ImmutableArray<Diagnostic> diagnostics = result.Diagnostics;

        Assert.IsTrue(
            diagnostics.IsEmpty,
            string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return GeneratorHelper.GetGeneratedSource(result.GeneratedSources, "Shaders.Shader");
    }

    /// <summary>
    /// Runs the generator over a source and joins the identifiers it reported.
    /// </summary>
    /// <param name="source">The source to compile.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The reported identifiers, in the order they were produced.</returns>
    private static string Report(string source, string assemblyName)
    {
        return string.Join(", ", Run(source, assemblyName).Diagnostics.Select(static diagnostic => diagnostic.Id));
    }

    /// <summary>
    /// Runs the generator over a source and returns the whole run result.
    /// </summary>
    /// <param name="source">The source to compile.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The result of the run.</returns>
    private static GeneratorRunResult Run(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return result;
    }
}
