using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComputeWeave.D2D1.SourceGenerators;
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
        return Texts.Value.Any(text => text.Contains(id, StringComparison.Ordinal));
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

    /// <summary>
    /// Every piece of text this assembly carries, read from its compiled form.
    /// </summary>
    /// <remarks>
    /// An identifier reaches a test either as a literal in a method body or as an argument of an attribute
    /// that drives it, so both are read. This type is skipped, because the identifiers it names are the
    /// exemptions above and those are bookkeeping rather than an assertion.
    /// </remarks>
    private static readonly Lazy<HashSet<string>> Texts = new(static () =>
    {
        HashSet<string> texts = [];

        foreach (Type type in typeof(Test_DiagnosticCoverage).Assembly.GetTypes())
        {
            if (type == typeof(Test_DiagnosticCoverage))
            {
                continue;
            }

            foreach (MethodBase method in Members(type))
            {
                foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                {
                    foreach (CustomAttributeTypedArgument argument in attribute.ConstructorArguments)
                    {
                        Collect(argument, texts);
                    }
                }

                Collect(method, texts);
            }
        }

        return texts;
    });

    /// <summary>
    /// Every method, constructor and static initializer of a type.
    /// </summary>
    /// <param name="type">The type to enumerate.</param>
    /// <returns>The members that can carry a string literal.</returns>
    private static IEnumerable<MethodBase> Members(Type type)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (MethodInfo method in type.GetMethods(Flags))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(Flags))
        {
            yield return constructor;
        }

        if (type.TypeInitializer is { } initializer)
        {
            yield return initializer;
        }
    }

    /// <summary>
    /// Adds every string literal a method body loads.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <param name="texts">The set to add to.</param>
    /// <remarks>
    /// The scan tries every byte offset, so it can resolve a value that is not an operand. That only ever
    /// adds strings, which can hide a missing test but can never invent one, and the comparison against the
    /// mutation run was made with this same scan.
    /// </remarks>
    private static void Collect(MethodBase method, HashSet<string> texts)
    {
        byte[]? body = method.GetMethodBody()?.GetILAsByteArray();

        if (body is null)
        {
            return;
        }

        for (int i = 0; i < body.Length - 4; i++)
        {
            if (body[i] != 0x72)
            {
                continue;
            }

            try
            {
                _ = texts.Add(method.Module.ResolveString(BitConverter.ToInt32(body, i + 1)));
            }
            catch (ArgumentException)
            {
            }
        }
    }

    /// <summary>
    /// Adds every string an attribute argument carries.
    /// </summary>
    /// <param name="argument">The argument to read.</param>
    /// <param name="texts">The set to add to.</param>
    private static void Collect(CustomAttributeTypedArgument argument, HashSet<string> texts)
    {
        if (argument.Value is string text)
        {
            _ = texts.Add(text);
        }
        else if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> arguments)
        {
            foreach (CustomAttributeTypedArgument element in arguments)
            {
                Collect(element, texts);
            }
        }
    }
}
