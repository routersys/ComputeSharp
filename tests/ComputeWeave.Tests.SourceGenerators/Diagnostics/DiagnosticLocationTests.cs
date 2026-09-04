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
/// compilation moved out of the transform node, which is where the locations stop being tree bound. Such a
/// location is captured by value and resolved against the compilation when the diagnostic is created, a tree
/// being what the directives written in a file and the analyzer configuration entries for it are applied through.
/// </remarks>
[TestClass]
public class DiagnosticLocationTests
{
    // A shipped descriptor is borrowed because declaring one here would ask for analyzer release tracking
    private static readonly DiagnosticDescriptor Descriptor = DiagnosticDescriptors.MissingRequiresDoublePrecisionSupportAttribute;

    // The rows that have nothing to resolve are handed a compilation holding no tree at all
    private static readonly CSharpCompilation Empty = CSharpCompilation.Create("DiagnosticLocationEmpty");

    [TestMethod]
    public void ALocationInsideATreeIsReportedAtThatTree()
    {
        SyntaxTree tree = CompilationHelper.ParseTree("class Shader { }");
        SyntaxNode node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, node.GetLocation(), "Shader").ToDiagnostic(Empty);

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

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(Empty);

        Assert.AreEqual(LocationKind.ExternalFile, diagnostic.Location.Kind);
        Assert.AreEqual("Shader.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.AreEqual(lineSpan, diagnostic.Location.GetLineSpan().Span);
        Assert.AreEqual(span, diagnostic.Location.SourceSpan);
    }

    /// <summary>
    /// A captured location naming a file the compilation holds has to arrive bound to that file's tree.
    /// </summary>
    [TestMethod]
    public void ALocationNamingAFileTheCompilationHoldsIsReportedInThatTree()
    {
        CSharpCompilation compilation = CreateCompilationUnder("DiagnosticLocationInTree", ("Shader.cs", "class Shader { }"));
        SyntaxTree tree = compilation.SyntaxTrees.Single();
        SyntaxNode node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Location location = Location.Create("Shader.cs", node.Span, tree.GetLineSpan(node.Span).Span);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(compilation);

        Assert.AreEqual(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.AreSame(tree, diagnostic.Location.SourceTree);
        Assert.AreEqual(node.Span, diagnostic.Location.SourceSpan);
    }

    /// <summary>
    /// A compilation holds every file of a project, so a captured location has to reach the tree it names
    /// rather than whichever tree the compilation happens to hold first.
    /// </summary>
    /// <remarks>
    /// The rows above resolve against a compilation holding one tree, where a lookup that never asks what
    /// the file is called still lands on the right one for having nowhere else to land. The tree named here
    /// comes second, and the one before it is longer than the span, so it is what such a lookup would take.
    /// </remarks>
    [TestMethod]
    public void ALocationNamingOneOfSeveralFilesIsReportedInTheTreeItNames()
    {
        CSharpCompilation compilation = CreateCompilationUnder(
            "DiagnosticLocationAmongTrees",
            ("Other.cs", "class Other { } class Filler { } class Padding { }"),
            ("Shader.cs", "class Shader { }"));

        SyntaxTree named = compilation.SyntaxTrees.Last();
        SyntaxNode node = named.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        Location location = Location.Create("Shader.cs", node.Span, named.GetLineSpan(node.Span).Span);

        // The span has to fit the tree that comes first, or the row passes for the reason the next one reads
        Assert.IsTrue(node.Span.End <= compilation.SyntaxTrees.First().Length);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(compilation);

        Assert.AreEqual(LocationKind.SourceFile, diagnostic.Location.Kind);
        Assert.AreSame(named, diagnostic.Location.SourceTree);
    }

    /// <summary>
    /// A span reaching past the text names other text, and such a location throws where it is printed
    /// rather than where it is made, so the file is named instead.
    /// </summary>
    [TestMethod]
    public void ALocationWhoseSpanTheTreeDoesNotHoldIsReportedAtThatFile()
    {
        CSharpCompilation compilation = CreateCompilationUnder("DiagnosticLocationPastTheText", ("Shader.cs", "class Shader { }"));
        TextSpan span = TextSpan.FromBounds(4096, 4100);
        LinePositionSpan lineSpan = new(new LinePosition(64, 0), new LinePosition(64, 4));
        Location location = Location.Create("Shader.cs", span, lineSpan);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(compilation);

        Assert.AreEqual(LocationKind.ExternalFile, diagnostic.Location.Kind);
        Assert.AreEqual(span, diagnostic.Location.SourceSpan);
    }

    /// <summary>
    /// An empty path names no file, and it is the path every tree of a compilation parsed without one
    /// carries, so resolving on it would pick whichever tree came first.
    /// </summary>
    [TestMethod]
    public void ALocationNamingAnEmptyPathIsReportedAtThatFile()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation("class Shader { }", "DiagnosticLocationNoPath");
        TextSpan span = TextSpan.FromBounds(0, 16);
        LinePositionSpan lineSpan = new(new LinePosition(0, 0), new LinePosition(0, 16));
        Location location = Location.Create("", span, lineSpan);

        Assert.AreEqual("", compilation.SyntaxTrees.Single().FilePath);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(compilation);

        Assert.AreEqual(LocationKind.ExternalFile, diagnostic.Location.Kind);
    }

    [TestMethod]
    public void ALocationNamingNoFileIsReportedWithoutOne()
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation("class Shader { }", "DiagnosticLocationMetadata");
        Location location = compilation.GetSpecialType(SpecialType.System_Int32).Locations.First();

        Assert.AreEqual(LocationKind.MetadataFile, location.Kind);

        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, location, "Shader").ToDiagnostic(compilation);

        Assert.AreEqual(Location.None, diagnostic.Location);
    }

    [TestMethod]
    public void NoLocationIsReportedWithoutOne()
    {
        Diagnostic diagnostic = DiagnosticInfo.Create(Descriptor, (Location?)null, "Shader").ToDiagnostic(Empty);

        Assert.AreEqual(Location.None, diagnostic.Location);
    }

    /// <summary>
    /// Creates a compilation whose trees are parsed under paths a captured location can name.
    /// </summary>
    /// <param name="assemblyName">The name to give the assembly.</param>
    /// <param name="files">The path and the source of each tree, in the order the compilation holds them.</param>
    /// <returns>A compilation whose trees carry the paths given.</returns>
    private static CSharpCompilation CreateCompilationUnder(string assemblyName, params (string Path, string Source)[] files)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation([.. files.Select(static file => file.Source)], assemblyName);

        for (int i = 0; i < files.Length; i++)
        {
            SyntaxTree tree = compilation.SyntaxTrees.ElementAt(i);

            compilation = compilation.ReplaceSyntaxTree(
                tree,
                CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)tree.Options, files[i].Path));
        }

        return compilation;
    }
}
