using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// The analyzers that check how a shader type is declared, none of which had a test.
/// </summary>
/// <remarks>
/// Each of these reports an error, so an author meets them before anything is generated. The danger runs one
/// way: an analyzer that started reporting where it should not would break the build of this repository, while
/// one that stopped reporting breaks nothing here, no shader in this repository being declared the wrong way.
/// Every method below therefore builds the declaration the analyzer exists to refuse, and the last one shows
/// that all of them stay quiet on a shader that is declared correctly.
/// </remarks>
[TestClass]
public class ShaderDeclarationAnalyzerTests
{
    /// <summary>
    /// A shader that is declared the way the ones in this repository are, for the negative case.
    /// </summary>
    private const string WellFormedSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            [GloballyCoherent]
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = 1;
            }
        }
        """;

    [TestMethod]
    public void AShaderImplementingBothShaderInterfacesIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader, IComputeShader<Float4>
            {
                public void Execute()
                {
                }

                Float4 IComputeShader<Float4>.Execute()
                {
                    return default;
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new MultipleComputeShaderInterfacesOnShaderTypeAnalyzer(),
            [Source],
            "ShaderMultipleInterfacesAnalyzerTests",
            "CMPW0042");
    }

    [TestMethod]
    public void AShaderWithoutTheDescriptorAttributeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new MissingComputeShaderDescriptorOnComputeShaderAnalyzer(),
            [Source],
            "ShaderMissingDescriptorAnalyzerTests",
            "CMPW0053");
    }

    [TestMethod]
    public void TheDescriptorAttributeOnATypeThatIsNoShaderIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct NotAShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidGeneratedComputeShaderDescriptorAttributeTargetAnalyzer(),
            [Source],
            "ShaderInvalidDescriptorTargetAnalyzerTests",
            "CMPW0054");
    }

    [TestMethod]
    public void AShaderTypeTheAssemblyCannotReachIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal class Container
            {
                [ThreadGroupSize(DefaultThreadGroupSizes.X)]
                [GeneratedComputeShaderDescriptor]
                private readonly partial struct Shader : IComputeShader
                {
                    public void Execute()
                    {
                    }
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new NotAccessibleGeneratedComputeShaderDescriptorAttributeTargetAnalyzer(),
            [Source],
            "ShaderNotAccessibleTypeAnalyzerTests",
            "CMPW0055");
    }

    [TestMethod]
    public void AShaderFieldOfATypeTheAssemblyCannotReachIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            internal class Container
            {
                private struct Hidden
                {
                    public float Value;
                }

                [ThreadGroupSize(DefaultThreadGroupSizes.X)]
                [GeneratedComputeShaderDescriptor]
                internal readonly partial struct Shader : IComputeShader
                {
                    private readonly Hidden hidden;

                    public void Execute()
                    {
                    }
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new NotAccessibleFieldTypeInGeneratedShaderDescriptorAttributeTargetAnalyzer(),
            [Source],
            "ShaderNotAccessibleFieldTypeAnalyzerTests",
            "CMPW0056");
    }

    [TestMethod]
    public void AShaderWithFieldsThatIsNotReadOnlyIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal partial struct Shader : IComputeShader
            {
                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = 1;
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new NotReadOnlyComputeShaderTypeWithFieldsAnalyzer(),
            [Source],
            "ShaderNotReadOnlyAnalyzerTests",
            "CMPW0057");
    }

    [TestMethod]
    public void AGloballyCoherentFieldThatIsNoWritableBufferIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                [GloballyCoherent]
                private readonly float factor;

                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidGloballyCoherentFieldDeclarationAnalyzer(),
            [Source],
            "ShaderGloballyCoherentAnalyzerTests",
            "CMPW0058");
    }

    [TestMethod]
    public void AShaderWithoutAThreadGroupSizeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidThreadGroupSizeAttributeUseAnalyzer(),
            [Source],
            "ShaderMissingThreadGroupSizeAnalyzerTests",
            "CMPW0047");
    }

    [TestMethod]
    public void AThreadGroupSizeWithAnUndeclaredDefaultIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize((DefaultThreadGroupSizes)999)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidThreadGroupSizeAttributeUseAnalyzer(),
            [Source],
            "ShaderInvalidDefaultThreadGroupSizeAnalyzerTests",
            "CMPW0048");
    }

    [TestMethod]
    public void AThreadGroupSizeOutOfRangeIsDiagnosed()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(0, 0, 0)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidThreadGroupSizeAttributeUseAnalyzer(),
            [Source],
            "ShaderThreadGroupSizeOutOfRangeAnalyzerTests",
            "CMPW0044");
    }

    [TestMethod]
    public void AThreadGroupWithTooManyThreadsIsDiagnosed()
    {
        // Every axis is inside its own bound, and the group holds 2048 threads
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(32, 32, 2)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidThreadGroupSizeAttributeUseAnalyzer(),
            [Source],
            "ShaderThreadGroupTooManyThreadsAnalyzerTests",
            "CMPW0044");
    }

    [TestMethod]
    public void AThreadGroupAtTheThreadLimitIsNotDiagnosed()
    {
        // The same shape one thread group smaller, which the hardware accepts
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(32, 32, 1)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                public void Execute()
                {
                }
            }
            """;

        AnalyzerHelper.AssertDiagnostics(
            new InvalidThreadGroupSizeAttributeUseAnalyzer(),
            [Source],
            "ShaderThreadGroupAtThreadLimitAnalyzerTests");
    }

    [TestMethod]
    public void EveryAnalyzerIsQuietOnAWellFormedShader()
    {
        // Without this, each test above would pass for an analyzer that reported on everything it saw
        DiagnosticAnalyzer[] analyzers =
        [
            new MultipleComputeShaderInterfacesOnShaderTypeAnalyzer(),
            new MissingComputeShaderDescriptorOnComputeShaderAnalyzer(),
            new InvalidGeneratedComputeShaderDescriptorAttributeTargetAnalyzer(),
            new NotAccessibleGeneratedComputeShaderDescriptorAttributeTargetAnalyzer(),
            new NotAccessibleFieldTypeInGeneratedShaderDescriptorAttributeTargetAnalyzer(),
            new NotReadOnlyComputeShaderTypeWithFieldsAnalyzer(),
            new InvalidGloballyCoherentFieldDeclarationAnalyzer(),
            new InvalidThreadGroupSizeAttributeUseAnalyzer()
        ];

        foreach (DiagnosticAnalyzer analyzer in analyzers)
        {
            AnalyzerHelper.AssertDiagnostics(analyzer, [WellFormedSource], "ShaderWellFormed" + analyzer.GetType().Name);
        }
    }
}
