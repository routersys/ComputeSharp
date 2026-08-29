using System.Linq;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Which types the discovery refuses. A native integer type has no HLSL counterpart and has to be refused
/// like every other .NET integer that is not mapped.
/// </summary>
/// <remarks>
/// <para>
/// The refusal keys on the name a type carries in metadata. It used to key on the name the type displays as,
/// and a native integer type displays as the keyword rather than as <c>System.IntPtr</c>, so those two alone
/// were taken for a custom struct: the generated HLSL declared a struct for them and then wrote the keyword
/// at the use site, which the shader compiler rejects while naming generated code the author never wrote.
/// </para>
/// <para>
/// The last test is the control. Refusing every type whose metadata name begins with <c>System.</c> must not
/// refuse a struct the author declared, which is what the discovery exists to collect.
/// </para>
/// </remarks>
[TestClass]
public class NativeIntegerTypeTests
{
    private const string NativeIntegerSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                nint value = 1000;

                this.buffer[0] = (float)value;
            }
        }
        """;

    private const string UnsignedNativeIntegerSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                nuint value = 1000;

                this.buffer[0] = (float)value;
            }
        }
        """;

    /// <summary>
    /// A .NET integer that was refused before as well, so the two cases can be told apart.
    /// </summary>
    private const string SixteenBitIntegerSource = """
        using ComputeWeave;

        namespace Shaders;

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                short value = 1000;

                this.buffer[0] = (float)value;
            }
        }
        """;

    /// <summary>
    /// A struct the author declared, which the discovery must keep collecting.
    /// </summary>
    private const string CustomStructSource = """
        using ComputeWeave;

        namespace Shaders;

        internal struct Helper
        {
            public float Value;

            public Helper(float value)
            {
                Value = value;
            }

            public readonly float Doubled() => Value * 2;
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        internal readonly partial struct Shader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> buffer;

            public void Execute()
            {
                Helper helper = new(2.0f);

                this.buffer[0] = helper.Doubled();
            }
        }
        """;

    [TestMethod]
    public void ANativeIntegerIsRefused()
    {
        AssertReports(NativeIntegerSource, "NativeIntegerTests", "CMPW0050");
    }

    [TestMethod]
    public void AnUnsignedNativeIntegerIsRefused()
    {
        AssertReports(UnsignedNativeIntegerSource, "UnsignedNativeIntegerTests", "CMPW0050");
    }

    [TestMethod]
    public void ASixteenBitIntegerIsRefused()
    {
        AssertReports(SixteenBitIntegerSource, "SixteenBitIntegerTests", "CMPW0050");
    }

    [TestMethod]
    public void ADeclaredStructIsAccepted()
    {
        AssertNoDiagnostics(CustomStructSource, "CustomStructTests");
    }

    private static void AssertReports(string source, string assemblyName, string expectedId)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.IsTrue(actualIds.Contains(expectedId), $"{expectedId} is not reported: {string.Join(", ", actualIds)}");
    }

    private static void AssertNoDiagnostics(string source, string assemblyName)
    {
        string[] actualIds = Run(source, assemblyName);

        Assert.AreEqual(0, actualIds.Length, string.Join(", ", actualIds));
    }

    private static string[] Run(string source, string assemblyName)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id).Distinct().Order()];
    }
}
