using System.Collections.Immutable;
using ComputeWeave.SourceGeneration.Models;
using ComputeWeave.SourceGeneration.SyntaxProcessors;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Tests that the two implementations of the constant buffer size computation agree.
/// </summary>
/// <remarks>
/// <para>
/// The descriptor generator walks the captured fields to build the layout, and the analyzer that
/// reports an exceeded dispatch data size walks them again through a reduced version that only
/// tracks the rolling size. They are separate code paths over the same packing rules, so a change
/// applied to one and not the other moves the size the analyzer sees away from the size the shader
/// is actually given, and the analyzer then reports on a layout that is not the one being built.
/// </para>
/// <para>
/// There are three packing rules and a shader is declared for each, because a divergence in one of
/// them is absorbed by the others. A field straddling a boundary is realigned, but a matrix placed
/// after it starts a fresh 16 byte row either way, so the two sizes come back together. Each shader
/// below therefore exercises one rule with nothing after it that would hide the difference.
/// </para>
/// </remarks>
[TestClass]
public class ConstantBufferSizeParityTests
{
    /// <summary>
    /// A shader whose captured field straddles a 16 byte boundary.
    /// </summary>
    private const string StraddlingFieldSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;
            private readonly Float3 straddling;
            private readonly float trailing;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = this.straddling.X + this.trailing;
            }
        }
        """;

    /// <summary>
    /// A shader capturing a nested struct whose first field is a scalar.
    /// </summary>
    private const string NestedStructSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Nested
        {
            public float first;
            public Float2 rest;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;
            private readonly Float3 straddling;
            private readonly Nested nested;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = this.straddling.X + this.nested.first;
            }
        }
        """;

    /// <summary>
    /// A shader capturing a matrix with more than one row, each of which is aligned as its own register.
    /// </summary>
    private const string NonLinearMatrixSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;
            private readonly Float2x3 matrix;
            private readonly float trailing;

            public void Execute()
            {
                this.buffer[ThreadIds.X] = this.matrix.M11 + this.trailing;
            }
        }
        """;

    [TestMethod]
    public void AStraddlingFieldIsSizedTheSameByBothPaths()
    {
        AssertBothPathsAgree(StraddlingFieldSource, "ConstantBufferSizeParityStraddlingTests");
    }

    [TestMethod]
    public void ANestedStructIsSizedTheSameByBothPaths()
    {
        AssertBothPathsAgree(NestedStructSource, "ConstantBufferSizeParityNestedTests");
    }

    [TestMethod]
    public void ANonLinearMatrixIsSizedTheSameByBothPaths()
    {
        AssertBothPathsAgree(NonLinearMatrixSource, "ConstantBufferSizeParityMatrixTests");
    }

    /// <summary>
    /// Runs both size computations over the same shader type and checks that they arrive at the same size.
    /// </summary>
    /// <param name="source">The source declaring the shader type to size.</param>
    /// <param name="assemblyName">The name to give the compilation.</param>
    private static void AssertBothPathsAgree(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(source, assemblyName);
        INamedTypeSymbol? typeSymbol = compilation.GetTypeByMetadataName("Shaders.Shader");

        Assert.IsNotNull(typeSymbol);

        // Both paths start from the prologue the generator uses for a shader that is not pixel-shader-like
        int sizeFromGenerator = sizeof(int) * 3;
        int sizeFromAnalyzer = sizeof(int) * 3;

        ConstantBufferSyntaxProcessor.GetInfo(compilation, typeSymbol, ref sizeFromGenerator, out ImmutableArray<FieldInfo> fields);
        ConstantBufferSyntaxProcessor.GetInfo(compilation, typeSymbol, ref sizeFromAnalyzer);

        // Guard against a shader above silently ceasing to capture anything, which would make the sizes agree for the wrong reason
        Assert.AreNotEqual(0, fields.Length);
        Assert.AreEqual(sizeFromGenerator, sizeFromAnalyzer);
    }
}
