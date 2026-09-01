using System;
using System.Linq;
using System.Reflection;
using ComputeWeave.SourceGeneration.Mappings;
using ComputeWeave.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Shaders;

/// <summary>
/// Tests for the set of C# syntax kinds a shader body may use.
/// </summary>
/// <remarks>
/// The set is measured, not designed, so these tests pin what the measurement found. A shader written the way
/// the ones in this repository are written must report nothing: that is what makes it safe to raise the severity
/// later. A shader written with syntax the set does not cover must report it, and the report must not refuse it.
/// </remarks>
[TestClass]
public class AcceptedSyntaxSetTests
{
    [TestMethod]
    public void AShaderWrittenTheUsualWayReportsNothing()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private const float Scale = 2.5f;

                private readonly ReadWriteBuffer<float> buffer;

                private readonly float factor;

                public void Execute()
                {
                    float total = 0;

                    for (int i = 0; i < 4; i++)
                    {
                        if (i % 2 == 0)
                        {
                            total += Scale * this.factor;
                        }
                        else
                        {
                            total -= Hlsl.Abs(i);
                        }
                    }

                    Float4 color = new(total, total * 2, 3, 4);
                    Int4 rounded = (Int4)color;

                    switch (rounded.X)
                    {
                        case 0:
                            total = 1;
                            break;
                        default:
                            total += rounded.Y;
                            break;
                    }

                    static float Twice(float value) => value * 2;

                    this.buffer[ThreadIds.X] = Twice(total);
                }
            }
            """;

        Diagnostic[] reports = Run(Source, "ShaderAcceptedSyntaxTests");

        Assert.AreEqual(0, reports.Length, string.Join(", ", reports.Select(static report => report.GetMessage())));
    }

    [TestMethod]
    [DataRow("float v = k switch { 1 => 1.0f, _ => 2.0f };", "ShaderSwitchExpressionSyntaxTests", "SwitchExpression")]
    [DataRow("float v = k is 1 ? 3.0f : 4.0f;", "ShaderIsPatternSyntaxTests", "IsPatternExpression")]
    [DataRow("float v = 5; v = v!;", "ShaderSuppressNullableSyntaxTests", "SuppressNullableWarningExpression")]
    [DataRow("float v = 5; goto done; done: v += 1;", "ShaderGotoSyntaxTests", "GotoStatement")]
    public void SyntaxOutsideTheSetIsReported(string body, string assemblyName, string expected)
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

                    this.buffer[ThreadIds.X] = v;
                }
            }
            """;

        Diagnostic[] reports = Run(source, assemblyName);

        Assert.IsTrue(
            reports.Any(report => report.GetMessage().Contains(expected)),
            string.Join(", ", reports.Select(static report => report.GetMessage())));
    }

    [TestMethod]
    public void SyntaxInAStaticFieldInitializerIsReported()
    {
        const string Source = """
            using ComputeWeave;

            namespace Shaders;

            [ThreadGroupSize(DefaultThreadGroupSizes.X)]
            [GeneratedComputeShaderDescriptor]
            internal readonly partial struct Shader : IComputeShader
            {
                private static readonly float Scale = Hlsl.Abs(-2.0f) switch { 0 => 1.0f, _ => 2.0f };

                private readonly ReadWriteBuffer<float> buffer;

                public void Execute()
                {
                    this.buffer[ThreadIds.X] = Scale;
                }
            }
            """;

        Diagnostic[] reports = Run(Source, "ShaderStaticFieldSyntaxTests");

        Assert.IsTrue(
            reports.Any(static report => report.GetMessage().Contains("SwitchExpression")),
            string.Join(", ", reports.Select(static report => report.GetMessage())));
    }

    [TestMethod]
    public void TheReportDoesNotRefuseTheInput()
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
                    int k = 1;

                    float v = 5;

                    v = v!;

                    this.buffer[ThreadIds.X] = v + k;
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(Source, "ShaderReportSeverityTests");
        Diagnostic[] reports = Filter(result);

        // Info is the one severity that changes no build: a warning is raised to an error in this repository
        Assert.AreEqual(1, reports.Length, string.Join(", ", result.Diagnostics.Select(static diagnostic => diagnostic.Id)));
        Assert.AreEqual(DiagnosticSeverity.Info, reports[0].Severity);

        // The descriptor is written all the same, so the report records the syntax rather than refusing it
        Assert.IsTrue(
            result.GeneratedSources.Any(static source => source.HintName.Contains("Shaders.Shader")),
            string.Join(", ", result.GeneratedSources.Select(static source => source.HintName)));
    }

    [TestMethod]
    public void AKindUsedManyTimesIsReportedOnce()
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
                    float v = 5;

                    v = v!;

                    v = v!;

                    this.buffer[ThreadIds.X] = v;
                }
            }
            """;

        Diagnostic[] reports = Run(Source, "ShaderRepeatedSyntaxTests");

        Assert.AreEqual(1, reports.Length, string.Join(", ", reports.Select(static report => report.GetMessage())));
    }

    [TestMethod]
    public void TheSetRefusesAKindItDoesNotContain()
    {
        // The set has to say no to something, or every check against it passes for the wrong reason
        Assert.IsFalse(HlslKnownSyntax.IsAccepted(SyntaxKind.SwitchExpression));
        Assert.IsTrue(HlslKnownSyntax.IsAccepted(SyntaxKind.SwitchStatement));

        // A kind the rewriter always refuses must stay out, or raising the severity later would accept it
        Assert.IsFalse(HlslKnownSyntax.IsAccepted(SyntaxKind.ForEachStatement));
        Assert.IsFalse(HlslKnownSyntax.IsAccepted(SyntaxKind.TryStatement));

        // 'this' is the one refusal that is conditional, so it is in the set: only a bare 'this' is refused
        Assert.IsTrue(HlslKnownSyntax.IsAccepted(SyntaxKind.ThisExpression));
    }

    [TestMethod]
    public void TheKindsLeftOutOfAFamilyTheSetDrawsFromArePinned()
    {
        (string Name, SyntaxKind[] Kinds)[] families =
        [
            ("AssignmentExpressionSyntax", Family(SyntaxFacts.GetAssignmentExpression)),
            ("BinaryExpressionSyntax", Family(SyntaxFacts.GetBinaryExpression)),

            // 'default' carries no operator token, so the map that walks the tokens does not reach it
            ("LiteralExpressionSyntax", [.. Family(SyntaxFacts.GetLiteralExpression), SyntaxKind.DefaultLiteralExpression]),

            // This type has no map of its own, and carries these two kinds and no others
            ("MemberAccessExpressionSyntax", [SyntaxKind.SimpleMemberAccessExpression, SyntaxKind.PointerMemberAccessExpression]),
            ("PostfixUnaryExpressionSyntax", Family(SyntaxFacts.GetPostfixUnaryExpression)),
            ("PrefixUnaryExpressionSyntax", Family(SyntaxFacts.GetPrefixUnaryExpression)),
        ];

        string[] expected =
        [
            "AssignmentExpressionSyntax.CoalesceAssignmentExpression",
            "BinaryExpressionSyntax.AsExpression",
            "BinaryExpressionSyntax.CoalesceExpression",
            "BinaryExpressionSyntax.IsExpression",
            "LiteralExpressionSyntax.ArgListExpression",
            "LiteralExpressionSyntax.NullLiteralExpression",
            "LiteralExpressionSyntax.StringLiteralExpression",
            "LiteralExpressionSyntax.Utf8StringLiteralExpression",
            "MemberAccessExpressionSyntax.PointerMemberAccessExpression",
            "PostfixUnaryExpressionSyntax.SuppressNullableWarningExpression",
            "PrefixUnaryExpressionSyntax.AddressOfExpression",
            "PrefixUnaryExpressionSyntax.IndexExpression",
            "PrefixUnaryExpressionSyntax.PointerIndirectionExpression"
        ];

        string[] actual =
        [
            .. families
                .SelectMany(family => family.Kinds
                    .Where(static kind => !HlslKnownSyntax.IsAccepted(kind))
                    .Select(kind => $"{family.Name}.{kind}"))
                .Order(StringComparer.Ordinal)
        ];

        CollectionAssert.AreEqual(expected, actual, string.Join(", ", actual));

        // A family the set draws from nothing from is not one of these, and would make the list above vacuous
        foreach ((string name, SyntaxKind[] kinds) in families)
        {
            Assert.IsTrue(kinds.Any(HlslKnownSyntax.IsAccepted), name);
        }
    }

    [TestMethod]
    public void EveryNodeTypeThatCanCarrySeveralKindsIsAccountedFor()
    {
        // A node type carries more than one kind exactly when the factory for it takes the kind as an argument
        string[] actual =
        [
            .. typeof(SyntaxFactory)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static method => typeof(SyntaxNode).IsAssignableFrom(method.ReturnType))
                .Where(static method => method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(SyntaxKind) && parameter.Name == "kind"))
                .Select(static method => method.ReturnType.Name)
                .Distinct()
                .Order(StringComparer.Ordinal)
        ];

        string[] expected =
        [
            "AccessorDeclarationSyntax",
            "AssignmentExpressionSyntax",
            "BinaryExpressionSyntax",
            "BinaryPatternSyntax",
            "CheckedExpressionSyntax",
            "CheckedStatementSyntax",
            "ClassOrStructConstraintSyntax",
            "ConstructorInitializerSyntax",
            "DocumentationCommentTriviaSyntax",
            "GotoStatementSyntax",
            "InitializerExpressionSyntax",
            "LiteralExpressionSyntax",
            "MemberAccessExpressionSyntax",
            "OrderingSyntax",
            "PostfixUnaryExpressionSyntax",
            "PrefixUnaryExpressionSyntax",
            "RecordDeclarationSyntax",
            "TypeDeclarationSyntax",
            "YieldStatementSyntax"
        ];

        CollectionAssert.AreEqual(expected, actual, string.Join(", ", actual));
    }

    /// <summary>
    /// Collects the syntax kinds one node type carries, from the map that takes an operator token to them.
    /// </summary>
    /// <param name="map">The map from an operator token to the kind of the expression it forms.</param>
    /// <returns>The kinds the map reaches.</returns>
    private static SyntaxKind[] Family(Func<SyntaxKind, SyntaxKind> map)
    {
        return
        [
            .. Enum.GetValues<SyntaxKind>()
                .Select(map)
                .Where(static kind => kind != SyntaxKind.None)
                .Distinct()
        ];
    }

    /// <summary>
    /// Runs the generator over a source and returns the reports for syntax outside the accepted set.
    /// </summary>
    /// <param name="source">The source to compile.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The diagnostics that report syntax outside the accepted set.</returns>
    private static Diagnostic[] Run(string source, string assemblyName)
    {
        return Filter(RunGenerator(source, assemblyName));
    }

    /// <summary>
    /// Runs the generator over a source and returns the whole run result.
    /// </summary>
    /// <param name="source">The source to compile.</param>
    /// <param name="assemblyName">The assembly name to compile under.</param>
    /// <returns>The result of the run.</returns>
    private static GeneratorRunResult RunGenerator(string source, string assemblyName)
    {
        // Unsafe blocks have to be enabled, or the generator returns before it reaches a shader body
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(
            [source],
            assemblyName,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new ComputeShaderDescriptorGenerator());
        GeneratorRunResult result = driver.RunGenerators(compilation).GetRunResult().Results[0];

        Assert.IsNull(result.Exception, result.Exception?.ToString());

        return result;
    }

    /// <summary>
    /// Takes the reports for syntax outside the accepted set out of a run result.
    /// </summary>
    /// <param name="result">The result of the run.</param>
    /// <returns>The diagnostics that report syntax outside the accepted set.</returns>
    private static Diagnostic[] Filter(GeneratorRunResult result)
    {
        return [.. result.Diagnostics.Where(static diagnostic => diagnostic.Id == "CMPW0121")];
    }
}
