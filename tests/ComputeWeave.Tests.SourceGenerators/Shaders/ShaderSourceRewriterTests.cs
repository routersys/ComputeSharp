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

    /// <summary>
    /// An indexer declared on a custom type. HLSL has no indexers of its own, so the accessor the author
    /// wrote never runs, and the access used to be written out as it stands.
    /// </summary>
    [TestMethod]
    public void UsingAnIndexerOfACustomTypeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Values
            {
                public float Amount;

                public readonly float this[int index] => Amount;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Values values = default;

                    this.buffer[ThreadIds.X] = values[0];
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderCustomTypeIndexerTests", "CMPW0116");
    }

    /// <summary>
    /// An inline array, which has no indexer of its own: the access resolves through a span. The report is
    /// keyed on the type being indexed rather than on the indexer, so this shape is reached the same way.
    /// </summary>
    [TestMethod]
    public void UsingAnInlineArrayIndexerIsDiagnosed()
    {
        const string Source = """
            using System.Runtime.CompilerServices;
            using ComputeWeave;

            namespace Shaders;

            [InlineArray(4)]
            internal struct Values
            {
                private float element;
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Values values = default;

                    values[0] = 2.0f;

                    this.buffer[ThreadIds.X] = values[0];
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderInlineArrayIndexerTests", "CMPW0116");
    }

    /// <summary>
    /// The resource indexers. Only the ones taking separate coordinates are rewritten, so the rest share the
    /// fall through with the indexers that have to be reported, and the report has to tell them apart.
    /// </summary>
    [TestMethod]
    public void IndexingAResourceIsNotDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private readonly ReadOnlyBuffer<float> source;

                private readonly ConstantBuffer<float> constants;

                private readonly ReadWriteTexture2D<float> surface;

                private readonly ReadWriteTexture3D<float> volume;

                public void Execute()
                {
                    float value = this.source[0] + this.constants[0];

                    value += this.surface[0, 0] + this.surface[new Int2(0, 0)];
                    value += this.volume[0, 0, 0] + this.volume[new Int3(0, 0, 0)];

                    this.buffer[ThreadIds.X] = value;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderResourceIndexerTests", "CMPW0116");
    }

    /// <summary>
    /// The element access on a vector, the row access on a matrix and the swizzled indexer on a matrix.
    /// </summary>
    [TestMethod]
    public void IndexingAVectorOrAMatrixIsNotDiagnosed()
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
                    Float4 vector = default;
                    Float2x2 matrix = new(1, 2, 3, 4);

                    float value = vector[0] + matrix[0].X + matrix[MatrixIndex.M11, MatrixIndex.M12].Y;

                    this.buffer[ThreadIds.X] = value;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderVectorAndMatrixIndexerTests", "CMPW0116");
    }

    /// <summary>
    /// A group shared array, which is declared in HLSL as an array and needs no mapping.
    /// </summary>
    [TestMethod]
    public void IndexingAGroupSharedArrayIsNotDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                [GroupShared]
                private static readonly float[] Cache = new float[64];

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    Cache[ThreadIds.X] = 2.0f;

                    this.buffer[ThreadIds.X] = Cache[ThreadIds.X];
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderGroupSharedArrayIndexerTests", "CMPW0116");
    }

    /// <summary>
    /// A swizzled matrix indexer whose arguments are not constants. That already has a diagnostic of its own,
    /// and the branch reporting it falls through to the element access report, which must not add a second.
    /// </summary>
    [TestMethod]
    public void IndexingAMatrixWithANonConstantSwizzleIsNotReportedTwice()
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
                    Float2x2 matrix = new(1, 2, 3, 4);
                    MatrixIndex row = MatrixIndex.M11;

                    this.buffer[ThreadIds.X] = matrix[row, MatrixIndex.M12].X;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderNonConstantSwizzleTests", "CMPW0037");
        AssertIsNotDiagnosed(Source, "ShaderNonConstantSwizzleTests", "CMPW0116");
    }

    /// <summary>
    /// A generic static method. HLSL has no type parameters, so importing the declaration used to carry the
    /// type parameter list into the generated source.
    /// </summary>
    [TestMethod]
    public void CallingAGenericMethodIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static float First<T>(T value)
                    where T : unmanaged
                {
                    return 1.0f;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helper.First(1.0f);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderGenericMethodTests", "CMPW0117");
    }

    /// <summary>
    /// A generic instance method on a custom struct, which takes the other branch of the invocation path.
    /// </summary>
    [TestMethod]
    public void CallingAGenericInstanceMethodIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal struct Values
            {
                public float Amount;

                public readonly float First<T>(T value)
                    where T : unmanaged
                {
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
                    Values values = default;

                    this.buffer[ThreadIds.X] = values.First(1.0f);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderGenericInstanceMethodTests", "CMPW0117");
    }

    /// <summary>
    /// A generic local function, which is lifted to a top level HLSL function under a rewritten name. The
    /// declaration answers for it rather than the call, so the call names no second place to fix.
    /// </summary>
    [TestMethod]
    public void CallingAGenericLocalFunctionIsDiagnosed()
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
                    static float First<T>(T value)
                        where T : unmanaged
                    {
                        return 1.0f;
                    }

                    this.buffer[ThreadIds.X] = First(1.0f);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderGenericLocalFunctionTests", "CMPW0122");
    }

    /// <summary>
    /// The same call, read for what the call site does not report. The declaration answers for the
    /// function, so the refusal for a generic call leaves a local function alone.
    /// </summary>
    [TestMethod]
    public void CallingAGenericLocalFunctionIsNotDiagnosedAtTheCall()
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
                    static float First<T>(T value)
                        where T : unmanaged
                    {
                        return 1.0f;
                    }

                    this.buffer[ThreadIds.X] = First(1.0f);
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderGenericLocalFunctionCallTests", "CMPW0117");
    }

    /// <summary>
    /// The same call, reached through a static field initializer rather than the shader body. The two
    /// rewriters walk their invocations separately, so both have to answer the same way.
    /// </summary>
    [TestMethod]
    public void CallingAGenericMethodFromAStaticFieldIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static float First<T>(T value)
                    where T : unmanaged
                {
                    return 1.0f;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Helper.First(2.0f);

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderStaticFieldGenericMethodTests", "CMPW0117");
    }

    /// <summary>
    /// An intrinsic and a method of the author that are not generic. The check runs before any mapping is
    /// tried, so an intrinsic has to stay untouched by it.
    /// </summary>
    [TestMethod]
    public void CallingANonGenericMethodIsNotDiagnosed()
    {
        const string Source = """
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
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Hlsl.Abs(-2.0f) + Helper.Twice(1.0f);
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderNonGenericMethodTests", "CMPW0117");
    }

    /// <summary>
    /// A generic local function that is never called. It is lifted just the same, so the declaration has to
    /// answer for it: nothing reaches the call site to report.
    /// </summary>
    [TestMethod]
    public void DeclaringAGenericLocalFunctionWithoutCallingItIsDiagnosed()
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
                    static float First<T>(T value)
                        where T : unmanaged
                    {
                        return 1.0f;
                    }

                    this.buffer[ThreadIds.X] = 2.0f;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderUncalledGenericLocalFunctionTests", "CMPW0122");
    }

    /// <summary>
    /// A generic local function inside a method the shader imports. A nested rewriter walks that body, so what
    /// this pins is that the declaration answers there too and not only in the shader own body.
    /// </summary>
    [TestMethod]
    public void DeclaringAGenericLocalFunctionInsideAnImportedMethodIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal static class Helper
            {
                public static float Twice(float value)
                {
                    static float First<T>(T inner)
                    {
                        return 1.0f;
                    }

                    return value * 2;
                }
            }

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Helper.Twice(1.0f);
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderImportedGenericLocalFunctionTests", "CMPW0122");
    }

    /// <summary>
    /// A local function with no type parameters, which is the control for the two above.
    /// </summary>
    [TestMethod]
    public void DeclaringANonGenericLocalFunctionIsNotDiagnosed()
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
                    static float First(float value)
                    {
                        return value;
                    }

                    this.buffer[ThreadIds.X] = First(2.0f);
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderNonGenericLocalFunctionTests", "CMPW0122");
    }

    /// <summary>
    /// The length of a constant buffer, which is written to HLSL as the value it holds and has no dimensions.
    /// </summary>
    [TestMethod]
    public void ReadingTheLengthOfAConstantBufferIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private readonly ConstantBuffer<float> constants;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = this.constants.Length;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderConstantBufferLengthTests", "CMPW0118");
    }

    /// <summary>
    /// The same read, after a structured buffer in the same shader has claimed the accessor the two types
    /// share. The report is before the cache that claim fills, so the order of the two reads cannot matter.
    /// </summary>
    [TestMethod]
    public void ReadingTheLengthOfAConstantBufferAfterABufferIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private readonly ConstantBuffer<float> constants;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = this.buffer.Length + this.constants.Length;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderConstantBufferLengthAfterBufferTests", "CMPW0118");
    }

    /// <summary>
    /// The dimensions of the resources that do carry them, which are read through a generated helper.
    /// </summary>
    [TestMethod]
    public void ReadingTheDimensionsOfAResourceIsNotDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                private readonly ReadWriteTexture2D<float> surface;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = this.buffer.Length + this.surface.Width + this.surface.Height;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderResourceDimensionTests", "CMPW0118");
    }

    [TestMethod]
    [DataRow("ref float value = ref local", "ShaderRefLocalTests", "CMPW0022")]
    [DataRow("scoped ref float value = ref local", "ShaderScopedRefLocalTests", "CMPW0022")]
    public void DeclaringARefLocalIsDiagnosedWithoutFaultingTheGenerator(string declaration, string assemblyName, string diagnosticId)
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
                    float local = 1;

                    {{declaration}};

                    this.buffer[ThreadIds.X] = value;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(source, assemblyName, diagnosticId);
    }

    [TestMethod]
    [DataRow("System.Span<int> values = default", "ShaderSpanLocalTests", "CMPW0031")]
    [DataRow("scoped System.Span<int> values = default", "ShaderScopedSpanLocalTests", "CMPW0031")]
    public void DeclaringARefStructLocalIsDiagnosedWithoutFaultingTheGenerator(string declaration, string assemblyName, string diagnosticId)
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
                    {{declaration}};

                    this.buffer[ThreadIds.X] = values.Length;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(source, assemblyName, diagnosticId);
    }

    /// <summary>
    /// An intrinsic that writes through an out parameter, given an integer matrix. DXC terminates with an
    /// access violation on that combination, so the call is refused before the compiler is handed it.
    /// </summary>
    [TestMethod]
    public void GivingAnIntegerMatrixToAnIntrinsicWithAnOutParameterIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<int> buffer;

                public void Execute()
                {
                    Int2x2 fractional = Hlsl.Modf(new Int2x2(5, 5, 5, 5), out Int2x2 whole);

                    this.buffer[ThreadIds.X] = fractional.M22 + whole.M22;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderIntegerMatrixOutParameterTests", "CMPW0123");
    }

    /// <summary>
    /// The shapes that compile. One out parameter takes a scalar, an integer vector and a floating point
    /// matrix intact, and two of them take a scalar and a vector, so none of those is refused.
    /// </summary>
    [TestMethod]
    public void GivingAnIntrinsicWithAnOutParameterAShapeThatCompilesIsNotDiagnosed()
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
                    float scalar = Hlsl.Modf(3.75f, out float scalarWhole);
                    Int2 vector = Hlsl.Modf(new Int2(5, 5), out Int2 vectorWhole);
                    Float2x2 matrix = Hlsl.Modf(new Float2x2(3.5f, 3.5f, 3.5f, 3.5f), out Float2x2 matrixWhole);

                    Hlsl.SinCos(1.5f, out float scalarSin, out float scalarCos);
                    Hlsl.SinCos(new Float2(1.5f, 1.5f), out Float2 vectorSin, out Float2 vectorCos);

                    this.buffer[ThreadIds.X] = scalar + scalarWhole + vector.Y + vectorWhole.Y + matrix.M22 + matrixWhole.M22
                        + scalarSin + scalarCos + vectorSin.Y + vectorCos.Y;
                }
            }
            """;

        AssertIsNotDiagnosed(Source, "ShaderAllowedOutParameterShapesTests", "CMPW0123");
    }

    /// <summary>
    /// An intrinsic that writes through two out parameters, given a matrix. DXC terminates on that whatever
    /// the element type is, which one out parameter does not do for a floating point matrix.
    /// </summary>
    [TestMethod]
    public void GivingAMatrixToAnIntrinsicWithTwoOutParametersIsDiagnosed()
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
                    Hlsl.SinCos(new Float2x2(1, 1, 1, 1), out Float2x2 sin, out Float2x2 cos);

                    this.buffer[ThreadIds.X] = sin.M22 + cos.M22;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderMatrixTwoOutParametersTests", "CMPW0123");
    }

    /// <summary>
    /// The same call, reached through a static field initializer rather than the shader body. The two
    /// rewriters write intrinsic calls out separately, so both have to answer the same way. The out argument
    /// is another static field, which is the shape that reaches the compiler as a well formed call.
    /// </summary>
    [TestMethod]
    public void GivingAnIntegerMatrixToAnIntrinsicWithAnOutParameterFromAStaticFieldIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static Int2x2 Whole;

                private static readonly Int2x2 Fractional = Hlsl.Modf(new Int2x2(5, 5, 5, 5), out Whole);

                private readonly ReadWriteBuffer<int> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Fractional.M22 + Whole.M22;
                }
            }
            """;

        AssertIsDiagnosedWithoutFaulting(Source, "ShaderStaticFieldIntegerMatrixTests", "CMPW0123");
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
