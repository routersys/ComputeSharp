using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ComputeWeave.D2D1.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// Holds the metadata of every shipped diagnostic of the pixel shader generators to describing the
/// diagnostic it belongs to.
/// </summary>
/// <remarks>
/// The descriptors are declared per assembly, so the compute side reading its own says nothing about this
/// one. Four of the six titles that named another diagnostic were on this side, and so was the description
/// that had a word written twice.
/// </remarks>
[TestClass]
public class Test_DiagnosticMetadata
{
    /// <summary>
    /// A word written twice in a row, which every one of these strings would carry to an author.
    /// </summary>
    private static readonly Regex RepeatedWord = new(@"\b(\w+)\s+\1\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// A placeholder in a string that the message arguments never reach.
    /// </summary>
    private static readonly Regex Placeholder = new(@"\{\d+(?::[^}]*)?\}");

    /// <summary>
    /// Two diagnostics sharing a title means at least one of them is named after the other.
    /// </summary>
    /// <remarks>
    /// Roslyn does not require titles to be unique. What makes uniqueness the right rule here is that every
    /// title in this repository names the construct or the condition it reports, so two that match are a copy
    /// that was not finished rather than two rules that happen to share a name.
    /// </remarks>
    [TestMethod]
    public void NoTwoDiagnosticsShareATitle()
    {
        string[] shared =
        [
            .. Declared()
                .GroupBy(static descriptor => descriptor.Title.ToString(), StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => $"{group.Key}: {string.Join(", ", group.Select(static descriptor => descriptor.Id).Order())}")
                .Order()
        ];

        Assert.AreEqual(0, shared.Length, string.Join(" | ", shared));
    }

    /// <summary>
    /// The title, the message and the description all reach an author, so a slip of the pen in one is shipped.
    /// </summary>
    [TestMethod]
    public void NoShippedTextRepeatsAWord()
    {
        string[] repeated =
        [
            .. Declared()
                .SelectMany(static descriptor => new[]
                {
                    (descriptor.Id, Text: descriptor.Title.ToString()),
                    (descriptor.Id, Text: descriptor.MessageFormat.ToString()),
                    (descriptor.Id, Text: descriptor.Description.ToString())
                })
                .Select(static pair => (pair.Id, Match: RepeatedWord.Match(pair.Text)))
                .Where(static pair => pair.Match.Success)
                .Select(static pair => $"{pair.Id}: {pair.Match.Value}")
                .Order()
        ];

        Assert.AreEqual(0, repeated.Length, string.Join(" | ", repeated));
    }

    /// <summary>
    /// Only the message format is given the arguments, so a placeholder anywhere else is read as it stands.
    /// </summary>
    /// <remarks>
    /// Nothing fails when one is left in. The build stays green because the repository reports none of these,
    /// and the tests compare identifiers rather than text. Where it shows is the author's tooling: the error
    /// log written with the ErrorLog switch carries the description into whatever reads it, and two
    /// descriptions had been carrying a placeholder there since the fork point. Only the descriptors are read,
    /// so a suppression justification is outside this.
    /// </remarks>
    [TestMethod]
    public void OnlyTheMessageFormatCarriesAPlaceholder()
    {
        string[] carried =
        [
            .. Declared()
                .SelectMany(static descriptor => new[]
                {
                    (descriptor.Id, Field: "title", Text: descriptor.Title.ToString()),
                    (descriptor.Id, Field: "description", Text: descriptor.Description.ToString()),
                    (descriptor.Id, Field: "category", Text: descriptor.Category),
                    (descriptor.Id, Field: "helpLinkUri", Text: descriptor.HelpLinkUri)
                })
                .Where(static pair => pair.Text is not null && Placeholder.IsMatch(pair.Text))
                .Select(static pair => $"{pair.Id}: {pair.Field}")
                .Order()
        ];

        Assert.AreEqual(0, carried.Length, string.Join(" | ", carried));
    }

    /// <summary>
    /// The population has to be non-empty, or both rules above pass for having read nothing.
    /// </summary>
    /// <remarks>
    /// The bound is a floor against reading nothing or reading one type, and not a count of the population.
    /// Descriptors are added over time, so a bound near the current number would fail for the wrong reason.
    /// </remarks>
    [TestMethod]
    public void TheDeclaredDiagnosticsAreRead()
    {
        int declared = Declared().Count();

        Assert.IsTrue(declared >= 50, declared.ToString());
    }

    /// <summary>
    /// Every <see cref="DiagnosticDescriptor"/> the generators declare.
    /// </summary>
    /// <returns>The declared descriptors.</returns>
    private static IEnumerable<DiagnosticDescriptor> Declared()
    {
        const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        foreach (Type type in typeof(D2DPixelShaderDescriptorGenerator).Assembly.GetTypes())
        {
            foreach (FieldInfo field in type.GetFields(Fields))
            {
                if (field.FieldType == typeof(DiagnosticDescriptor) &&
                    field.GetValue(null) is DiagnosticDescriptor descriptor)
                {
                    yield return descriptor;
                }
            }
        }
    }
}
