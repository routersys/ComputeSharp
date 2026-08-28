using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

[TestClass]
public class ShaderSourceRewriterTests
{
    [TestMethod]
    public void DeclaringAnArrayWithVarIsDiagnosedWithoutFaultingTheGenerator()
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
                    var values = new int[4];

                    this.buffer[ThreadIds.X] = values[0];
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderVarArrayTests", "CMPW0031");
    }

    [TestMethod]
    public void DeclaringALambdaIsDiagnosedWithoutFaultingTheGenerator()
    {
        const string Source = """
            using System;
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Func<int, int> identity = static value => value;

                    this.buffer[ThreadIds.X] = identity(1);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderLambdaTests", "CMPW0031");
    }

    [TestMethod]
    public void DeclaringANonStaticLocalFunctionIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private static float Helper(float value) => value * 2;

                public void Execute()
                {
                    float Helper(float value) => value * 10;

                    this.buffer[ThreadIds.X] = Helper(3);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderNonStaticLocalFunctionTests", "CMPW0113");
    }

    [TestMethod]
    public void DeclaringANonStaticLocalFunctionInAnImportedMethodIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helpers
            {
                public static float Outer(float value)
                {
                    float Inner(float inner) => inner * value;

                    return Inner(2);
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helpers.Outer(3);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderNonStaticLocalFunctionInImportTests", "CMPW0113");
    }

    /// <summary>
    /// A matrix constructor whose argument has no conversion to the element type. The generator adds
    /// an explicit cast to every argument of a matrix constructor, and to know which type to cast to
    /// it reads the parameter the argument binds to. When overload resolution has failed there is no
    /// parameter to read, and the generator faulted rather than leaving the argument alone.
    /// </summary>
    /// <remarks>
    /// There is no ComputeWeave diagnostic here. The C# compiler reports the call as unresolved, and
    /// all the generator has to do is finish, so that the error the author sees is that one and not a
    /// compilation unit that lost every descriptor it was going to be given.
    /// </remarks>
    [TestMethod]
    public void AMatrixConstructorArgumentThatDoesNotBindDoesNotFaultTheGenerator()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private const decimal Number = 1m;

                public void Execute()
                {
                    Float2x2 values = new(Number, 1, 1, 1);

                    this.buffer[ThreadIds.X] = values.M11;
                }
            }
            """;

        AssertIsNotFaulting(Source, "ShaderUnboundMatrixArgumentTests");
    }

    /// <summary>
    /// A property read from a custom type. HLSL structs carry fields and no properties, so the property
    /// is left out of the generated struct, and the access used to be written out as it stands and fail
    /// in the HLSL compiler, naming generated code the author never wrote.
    /// </summary>
    [TestMethod]
    public void ReadingAPropertyOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public readonly float Doubled => Amount * 2;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Helper helper = default;

                    helper.Amount = 2;

                    this.buffer[ThreadIds.X] = helper.Doubled;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderCustomTypePropertyTests", "CMPW0114");
    }

    /// <summary>
    /// The same read, reached through a static field initializer rather than the shader body. The two
    /// rewriters visit member accesses separately, so both have to answer the same way.
    /// </summary>
    [TestMethod]
    public void ReadingAPropertyOfACustomTypeFromAStaticFieldIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static float Doubled => 4;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Helper.Doubled;

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderStaticFieldPropertyTests", "CMPW0114");
    }

    /// <summary>
    /// A custom type that declares a property the shader never reads. The type is written to HLSL field
    /// by field, so nothing is lost, and this has to keep compiling: the diagnostic belongs to the read.
    /// </summary>
    [TestMethod]
    public void DeclaringAPropertyOnACustomTypeThatIsNeverReadIsNotDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Helper
            {
                public float Amount;

                public readonly float Doubled => Amount * 2;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Helper helper = default;

                    helper.Amount = 2;

                    this.buffer[ThreadIds.X] = helper.Amount;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderUnreadCustomTypePropertyTests", "CMPW0114");
    }

    /// <summary>
    /// The properties that do map to HLSL. A swizzle, a vector component and a resource length all reach
    /// the same fall through, so the diagnostic must not claim them.
    /// </summary>
    [TestMethod]
    public void ReadingAPropertyThatMapsToHlslIsNotDiagnosed()
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
                    Float4 values = new(1, 2, 3, 4);

                    this.buffer[ThreadIds.X] = values.XY.X + values.W + this.buffer.Length;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderMappedPropertyTests", "CMPW0114");
    }

    /// <summary>
    /// A binary operator declared on a custom type. HLSL has no operator overloads, so the body the author
    /// wrote never runs, and the operation used to be written out as it stands.
    /// </summary>
    [TestMethod]
    public void UsingABinaryOperatorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static Value operator +(Value left, float right)
                {
                    Value result = default;

                    result.Amount = left.Amount + right;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = default;

                    value = value + 2;

                    this.buffer[ThreadIds.X] = value.Amount;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderBinaryOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// A compound assignment that resolves to a binary operator declared on a custom type.
    /// </summary>
    [TestMethod]
    public void UsingACompoundAssignmentOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static Value operator +(Value left, float right)
                {
                    Value result = default;

                    result.Amount = left.Amount + right;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = default;

                    value += 2;

                    this.buffer[ThreadIds.X] = value.Amount;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderCompoundAssignmentOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// A unary operator declared on a custom type.
    /// </summary>
    [TestMethod]
    public void UsingAUnaryOperatorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static Value operator -(Value operand)
                {
                    Value result = default;

                    result.Amount = -operand.Amount;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = default;

                    value = -value;

                    this.buffer[ThreadIds.X] = value.Amount;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderUnaryOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// An increment operator declared on a custom type. It reaches a different operation than the unary
    /// operators, so it needs its own case.
    /// </summary>
    [TestMethod]
    public void UsingAnIncrementOperatorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static Value operator ++(Value operand)
                {
                    Value result = default;

                    result.Amount = operand.Amount + 1;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = default;

                    value++;

                    this.buffer[ThreadIds.X] = value.Amount;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderIncrementOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// Conversion operators declared on custom types, in both directions and in both forms.
    /// </summary>
    /// <remarks>
    /// This is the one form that used to reach the GPU. HLSL converts between a struct and a scalar on its
    /// own, taking the first member or filling every member, so the explicit conversion compiled and the
    /// shader silently computed a different value than the same code in C#.
    /// </remarks>
    [TestMethod]
    public void UsingAConversionOperatorOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Explicit
            {
                public float First;

                public float Second;

                public static explicit operator float(Explicit value) => value.Second;
            }

            internal struct Implicit
            {
                public float Amount;

                public static implicit operator float(Implicit value) => value.Amount;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Explicit first = default;
                    Implicit second = default;

                    this.buffer[0] = (float)first;
                    this.buffer[1] = second;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderConversionOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// The operators that do reach HLSL. The built-in ones resolve no operator method at all, and the ones
    /// on the primitive types are either mapped to an intrinsic or left as they stand, so the diagnostic
    /// must not claim any of them.
    /// </summary>
    [TestMethod]
    public void UsingAnOperatorThatReachesHlslIsNotDiagnosed()
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
                    float scalar = 1 + 2;
                    Float4 vector = new Float4(1, 2, 3, 4) + new Float4(1, 1, 1, 1);
                    Float2x2 matrix = new Float2x2(1, 0, 0, 1) * new Float2x2(2, 0, 0, 2);

                    this.buffer[ThreadIds.X] = scalar + vector.X + matrix.M11;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderMappedOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// The same operator, reached through a static field initializer rather than the shader body. The two
    /// rewriters walk their declarations separately, so both have to answer the same way.
    /// </summary>
    [TestMethod]
    public void UsingAnOperatorOfACustomTypeFromAStaticFieldIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static float operator +(Value left, float right) => left.Amount + right;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = default(Value) + 2;

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderStaticFieldOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// The same operator, reached through the constructor of a custom type rather than the shader body.
    /// A constructor is rewritten on its own, so it needs a walk of its own.
    /// </summary>
    [TestMethod]
    public void UsingAnOperatorOfACustomTypeFromAConstructorIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public Value(float amount)
                {
                    Value other = default;

                    other = other + amount;

                    this.Amount = other.Amount;
                }

                public static Value operator +(Value left, float right)
                {
                    Value result = default;

                    result.Amount = left.Amount + right;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Value value = new(2);

                    this.buffer[ThreadIds.X] = value.Amount;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderConstructorOperatorTests", "CMPW0115");
    }

    /// <summary>
    /// The same operator, reached through a local function rather than the body that holds it. A local
    /// function is lifted to a top level HLSL function but is rewritten from within its declaration, so
    /// what this pins is that the walk of that declaration reaches into it.
    /// </summary>
    [TestMethod]
    public void UsingAnOperatorOfACustomTypeFromALocalFunctionIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Value
            {
                public float Amount;

                public static Value operator +(Value left, float right)
                {
                    Value result = default;

                    result.Amount = left.Amount + right;

                    return result;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    static float Inner()
                    {
                        Value value = default;

                        value = value + 2;

                        return value.Amount;
                    }

                    this.buffer[ThreadIds.X] = Inner();
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderLocalFunctionOperatorTests", "CMPW0115");
    }

    private static void AssertIsNotDiagnosed(string source, string assemblyName, string diagnosticId)
    {
        CSharpCompilation compilation = CompilationHelper
            .CreateCompilation([source], assemblyName)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsFalse(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }

    private static void AssertIsNotFaulting(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilationAllowingErrors(source, assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
    }

    private static void AssertIsDiagnosedWithoutFaulting(string source, string assemblyName, string diagnosticId)
    {
        CSharpCompilation compilation = CompilationHelper
            .CreateCompilation([source], assemblyName)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());
        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId),
            string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }
}
