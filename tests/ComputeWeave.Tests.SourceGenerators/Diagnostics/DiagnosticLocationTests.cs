using System.Linq;
using ComputeWeave.SourceGeneration.Diagnostics;
using ComputeWeave.SourceGeneration.Models;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.SourceGenerators.Diagnostics;

/// <summary>
/// What a <see cref="ComputeWeave.SourceGeneration.Models.DiagnosticInfo"/> keeps of the location it was handed.
/// </summary>
/// <remarks>
/// The model holds a syntax tree and a span, so a location that belongs to no tree used to be dropped and the
/// diagnostic reached the author with no position at all. Nothing handed it such a location until the shader
/// compilation moved out of the transform node, which is where the locations stop being tree bound.
/// </remarks>
[TestClass]
public class DiagnosticLocationTests
{
    // A shipped descriptor is borrowed because declaring one here would ask for analyzer release tracking
    private static readonly DiagnosticDescriptor Descriptor = DiagnosticDescriptors.MissingRequiresDoublePrecisionSupportAttribute;

    [TestMethod]
    public void ALocationInsideATreeIsReportedAtThatTree()
    {
        SyntaxTree tree = CompilationHelper.ParseTree("class Shader { }");
        SyntaxNode node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, node.GetLocation(), "Shader").ToDiagnostic();

        Assert.AreEqual(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.AreSame(tree, diagnostic.Location.SourceTree);
        Assert.AreEqual(node.Span, diagnostic.Location.SourceSpan);
    }

    [TestMethod]
    public void ALocationNamingAFileOutsideAnyTreeIsReportedAtThatFile()
    {
        TextSpan span = TextSpan.FromBounds(17, 23);
        LinePositionSpan lineSpan = new(new LinePosition(4, 8), new LinePosition(4, 14));
        Location location = Location.Create("Shader.cs", span, lineSpan);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic();

        Assert.AreEqual(LocationKind.ExternalFile, diagnostic.Location.Kind);
        Assert.AreEqual("Shader.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.AreEqual(lineSpan, diagnostic.Location.GetLineSpan().Span);
        Assert.AreEqual(span, diagnostic.Location.SourceSpan);
    }

    [TestMethod]
    public void ALocationNamingNoFileIsReportedWithoutOne()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation("class Shader { }", "DiagnosticLocationMetadata");
        Location location = compilation.GetSpecialType(SpecialType.System_Int32).Locations.First();

        Assert.AreEqual(LocationKind.MetadataFile, location.Kind);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic();

        Assert.AreEqual(Location.None, diagnostic.Location);
    }

    [TestMethod]
    public void NoLocationIsReportedWithoutOne()
    {
        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, (Location?)null, "Shader").ToDiagnostic();

        Assert.AreEqual(Location.None, diagnostic.Location);
    }
}
