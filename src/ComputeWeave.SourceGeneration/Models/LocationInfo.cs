using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ComputeWeave.SourceGeneration.Models;

/// <summary>
/// A model for a captured source location, to be used within equatable incremental models.
/// The location is captured by value (ie. with no <see cref="SyntaxTree"/> references), so
/// that models with a captured location will correctly compare as equal across unrelated
/// edits (and so that they will never keep alive (or leak) any stale compilation objects).
/// </summary>
/// <param name="FilePath">The path of the source file for the referenced location.</param>
/// <param name="TextSpan">The span for the referenced location.</param>
/// <param name="LineSpan">The line span for the referenced location.</param>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>
    /// Creates a new <see cref="LocationInfo"/> instance from an input <see cref="Location"/> value.
    /// </summary>
    /// <param name="location">The <see cref="Location"/> value to capture, if available.</param>
    /// <returns>A <see cref="LocationInfo"/> instance for <paramref name="location"/>, if a source location was available.</returns>
    public static LocationInfo? From(Location? location)
    {
        if (location is not { SourceTree: not null })
        {
            return null;
        }

        FileLinePositionSpan lineSpan = location.GetLineSpan();

        return new LocationInfo(lineSpan.Path, location.SourceSpan, lineSpan.Span);
    }

    /// <summary>
    /// Creates a new <see cref="LocationInfo"/> instance from an input <see cref="ISymbol"/> value.
    /// </summary>
    /// <param name="symbol">The <see cref="ISymbol"/> instance to capture the location for.</param>
    /// <returns>A <see cref="LocationInfo"/> instance for <paramref name="symbol"/>, if a source location was available.</returns>
    public static LocationInfo? From(ISymbol symbol)
    {
        return From(symbol.Locations.FirstOrDefault());
    }

    /// <summary>
    /// Creates a new <see cref="Location"/> instance with the state from this model.
    /// </summary>
    /// <returns>A new <see cref="Location"/> instance with the state from this model.</returns>
    public Location ToLocation()
    {
        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    /// <summary>
    /// Creates a new <see cref="Location"/> instance with the state from this model, bound to the tree holding it.
    /// </summary>
    /// <param name="compilation">The <see cref="Compilation"/> to look the source file up in.</param>
    /// <returns>A new <see cref="Location"/> instance with the state from this model.</returns>
    /// <remarks>
    /// A location belonging to no tree carries no directive state, so a warning reported at one is not silenced
    /// by the <c>#pragma</c> the author wrote around it, and the analyzer configuration entries for its file do
    /// not reach it either. The tree is looked up where the diagnostic is created rather than captured with the
    /// rest: a model holding one compares unequal across unrelated edits and keeps a stale compilation alive,
    /// which is what capturing a location by value exists to avoid.
    /// </remarks>
    public Location ToLocation(Compilation compilation)
    {
        // An empty path names no file, and every tree of a compilation parsed without one carries it
        if (FilePath.Length == 0)
        {
            return ToLocation();
        }

        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            // A span reaching past the text belongs to other text, which the compiler throws on when read
            if (tree.FilePath == FilePath && TextSpan.End <= tree.Length)
            {
                return Location.Create(tree, TextSpan);
            }
        }

        // A file this compilation does not hold has no tree to bind to, so the location stays as captured
        return ToLocation();
    }
}
