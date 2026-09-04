using System.Collections.Immutable;
using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// What a declaration split into a defining and an implementing part is imported as. C# runs the body of the
/// implementing part, so a shader has to compute what that body computes however the author split it.
/// </summary>
/// <remarks>
/// The declaration is read from the first syntax reference of the symbol, which for a partial member is the
/// defining part and carries no body. Each shader here is written twice, once split and once whole, and the
/// two are required to produce the same source: reading the split one alone would accept an import that
/// dropped the body, which the shader compiler answers for against generated code the author never wrote.
/// </remarks>
[TestClass]
public class PartialMemberImportTests
{
    private const string PartialStaticMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static partial class Helper
        {
            public static partial float Twice(float value);
        }

        internal static partial class Helper
        {
            public static partial float Twice(float value)
            {
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
                this.buffer[0] = Helper.Twice(2.0f);
            }
        }
        """;

    private const string PlainStaticMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static float Twice(float value)
            {
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
                this.buffer[0] = Helper.Twice(2.0f);
            }
        }
        """;

    private const string PartialMethodInitializerSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static partial class Helper
        {
            public static partial float Twice(float value);
        }

        internal static partial class Helper
        {
            public static partial float Twice(float value)
            {
                return value * 2;
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

    private const string PlainMethodInitializerSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static float Twice(float value)
            {
                return value * 2;
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

    private const string PartialInstanceMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal partial struct Helper
        {
            public float Amount;

            public partial float Doubled();
        }

        internal partial struct Helper
        {
            public partial float Doubled()
            {
                return Amount * 2;
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

                this.buffer[0] = helper.Doubled();
            }
        }
        """;

    private const string PlainInstanceMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Amount;

            public float Doubled()
            {
                return Amount * 2;
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

                this.buffer[0] = helper.Doubled();
            }
        }
        """;

    private const string PartialConstructorSource = """
        using ComputeWeave;

        namespace Shaders;

        internal partial struct Helper
        {
            public float Amount;

            public partial Helper(float amount);

            public static float Read(Helper helper) => helper.Amount;
        }

        internal partial struct Helper
        {
            public partial Helper(float amount)
            {
                Amount = amount;
            }
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

    private const string PlainConstructorSource = """
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

    private const string PartialConstructorInitializerSource = """
        using ComputeWeave;

        namespace Shaders;

        internal partial struct Helper
        {
            public float Amount;

            public partial Helper(float amount);

            public static float Read(Helper helper) => helper.Amount;
        }

        internal partial struct Helper
        {
            public partial Helper(float amount)
            {
                Amount = amount;
            }
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

    private const string PlainConstructorInitializerSource = """
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

    private const string PartialEntryPointSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public partial void Execute();
        }

        internal readonly partial struct Shader
        {
            public partial void Execute()
            {
                this.buffer[0] = 42;
            }
        }
        """;

    private const string PlainEntryPointSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[0] = 42;
            }
        }
        """;

    private const string HidingMethodSource = """
        using ComputeWeave;

        namespace Shaders;

        internal static class Helper
        {
            public static new float Twice(float value)
            {
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
                this.buffer[0] = Helper.Twice(2.0f);
            }
        }
        """;

    [TestMethod]
    public void APartialStaticMethodIsImportedLikeAPlainOne()
    {
        AssertSameAsPlain(PartialStaticMethodSource, PlainStaticMethodSource, "PartialStaticMethodTests");
    }

    [TestMethod]
    public void APartialMethodInAStaticFieldInitializerIsImportedLikeAPlainOne()
    {
        AssertSameAsPlain(PartialMethodInitializerSource, PlainMethodInitializerSource, "PartialMethodInitializerTests");
    }

    [TestMethod]
    public void APartialInstanceMethodIsImportedLikeAPlainOne()
    {
        AssertSameAsPlain(PartialInstanceMethodSource, PlainInstanceMethodSource, "PartialInstanceMethodTests");
    }

    [TestMethod]
    public void APartialConstructorIsImportedLikeAPlainOne()
    {
        AssertSameAsPlain(PartialConstructorSource, PlainConstructorSource, "PartialConstructorTests");
    }

    [TestMethod]
    public void APartialConstructorInAStaticFieldInitializerIsImportedLikeAPlainOne()
    {
        AssertSameAsPlain(PartialConstructorInitializerSource, PlainConstructorInitializerSource, "PartialConstructorInitializerTests");
    }

    /// <summary>
    /// The entry point itself, which is looked up the same way but from the generator rather than the rewriter.
    /// </summary>
    [TestMethod]
    public void APartialEntryPointIsWrittenLikeAPlainOne()
    {
        AssertSameAsPlain(PartialEntryPointSource, PlainEntryPointSource, "PartialEntryPointTests");
    }

    /// <summary>
    /// A modifier HLSL has no name for. It is not what the split declarations are about, but it reaches the
    /// generated source the same way: written out as it stands, it is refused against code the author never
    /// wrote, and the body beside it is correct.
    /// </summary>
    [TestMethod]
    public void AModifierHlslDoesNotKnowIsNotWrittenOut()
    {
        string generated = Generate(HidingMethodSource, "HidingMethodTests");

        Assert.IsFalse(generated.Contains("new float Shaders_Helper_Twice"), $"the modifier is written out:\n{generated}");
        Assert.IsTrue(generated.Contains("static float Shaders_Helper_Twice(float value)"), $"the declaration is not written:\n{generated}");
        Assert.IsTrue(generated.Contains("return value * 2;"), $"the body is not written:\n{generated}");
    }

    private static void AssertSameAsPlain(string split, string whole, string assemblyName)
    {
        Assert.AreEqual(Generate(whole, assemblyName), Generate(split, assemblyName));
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
