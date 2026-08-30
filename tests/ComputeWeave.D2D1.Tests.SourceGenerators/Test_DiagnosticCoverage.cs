using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComputeWeave.D2D1.SourceGenerators;
using ComputeWeave.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.D2D1.Tests.SourceGenerators;

/// <summary>
/// Holds every shipped diagnostic of the pixel shader generators to having a test that names it.
/// </summary>
/// <remarks>
/// <para>
/// A diagnostic that stops being reported breaks nothing here: the inputs it refuses appear in no shader of
/// this repository, so the solution still builds and every other test still passes. Only a test of its own
/// catches that, and until now nothing said one had to exist.
/// </para>
/// <para>
/// What is measured is that the identifier is named by this assembly, which is weaker than measuring that a
/// test asserts the diagnostic is produced. The two were compared over all 199 declared identifiers on
/// 2026-08-30 by rewriting each descriptor's id and running the suites: every identifier a test named failed
/// one, and no identifier that went unnamed failed any. The names are read from the compiled assembly and
/// not from the sources, so a mention in a comment or a <c>#pragma</c> does not count as coverage.
/// </para>
/// </remarks>
[TestClass]
public class Test_DiagnosticCoverage
{
    /// <summary>
    /// The identifiers that cannot have a test, and why.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new()
    {
        ["CMPWD2D0033"] = "Reported only when the shader compiler fails without a message, which no input produces.",
        ["CMPWD2D0053"] = "Reported only when the shader compiler fails without a message, which no input produces."
    };

    [TestMethod]
    public void EveryShippedDiagnosticIsNamedByATest()
    {
        string[] missing = [.. Declared().Where(id => !Exempt.ContainsKey(id) && !IsNamed(id)).Order()];

        Assert.AreEqual(0, missing.Length, $"No test names: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// An exemption for a descriptor that no longer exists is stale bookkeeping, so it has to go.
    /// </summary>
    [TestMethod]
    public void EveryExemptedDiagnosticIsStillDeclared()
    {
        HashSet<string> declared = [.. Declared()];
        string[] stale = [.. Exempt.Keys.Where(id => !declared.Contains(id)).Order()];

        Assert.AreEqual(0, stale.Length, $"Exempted but no longer declared: {string.Join(", ", stale)}");
    }

    /// <summary>
    /// An exemption that gained a test is no longer an exemption, so it has to go as well.
    /// </summary>
    [TestMethod]
    public void NoExemptedDiagnosticIsNamedByATest()
    {
        string[] covered = [.. Exempt.Keys.Where(IsNamed).Order()];

        Assert.AreEqual(0, covered.Length, $"Exempted but now named by a test: {string.Join(", ", covered)}");
    }

    /// <summary>
    /// Checks whether any text this assembly carries names a given identifier.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <returns>Whether <paramref name="id"/> is named.</returns>
    private static bool IsNamed(string id)
    {
        return AssemblyStringHelper
            .GetLoadableStrings(typeof(Test_DiagnosticCoverage).Assembly, typeof(Test_DiagnosticCoverage))
            .Any(text => text.Contains(id, StringComparison.Ordinal));
    }

    /// <summary>
    /// The identifier of every <see cref="DiagnosticDescriptor"/> the generators declare.
    /// </summary>
    /// <returns>The declared identifiers.</returns>
    private static IEnumerable<string> Declared()
    {
        const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        foreach (Type type in typeof(D2DPixelShaderDescriptorGenerator).Assembly.GetTypes())
        {
            foreach (FieldInfo field in type.GetFields(Fields))
            {
                if (field.FieldType == typeof(DiagnosticDescriptor) &&
                    field.GetValue(null) is DiagnosticDescriptor descriptor)
                {
                    yield return descriptor.Id;
                }
            }
        }
    }
}
