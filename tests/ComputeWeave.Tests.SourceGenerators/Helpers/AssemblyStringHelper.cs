using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace ComputeWeave.Tests.SourceGenerators.Helpers;

/// <summary>
/// Reads the strings an assembly's code can load, from its compiled form.
/// </summary>
/// <remarks>
/// <para>
/// A value reaches code either as a literal a method body loads or as an argument of an attribute that
/// drives it, so both are read. A constant that no code names has its value in the constant table instead
/// and becomes neither, which is what lets a caller tell an assembly that can produce a value from one that
/// merely declares it. Reading the compiled form rather than the sources also leaves out what a comment or
/// a <c>#pragma</c> mentions.
/// </para>
/// <para>
/// The body scan tries every byte offset, so it can resolve a value that is not an operand. Such a value is
/// still one the assembly holds, because a token only resolves to a string that is already there.
/// </para>
/// </remarks>
internal static class AssemblyStringHelper
{
    /// <summary>
    /// The result for each assembly already read, as reading one is not cheap.
    /// </summary>
    private static readonly ConcurrentDictionary<(Assembly Assembly, Type? ExcludedType), HashSet<string>> Results = new();

    /// <summary>
    /// Gets every string the code of an assembly can load.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <param name="excludedType">A type to leave out, for a caller whose own strings are bookkeeping.</param>
    /// <returns>The strings the assembly's code can load.</returns>
    public static HashSet<string> GetLoadableStrings(Assembly assembly, Type? excludedType = null)
    {
        return Results.GetOrAdd((assembly, excludedType), static key => Read(key.Assembly, key.ExcludedType));
    }

    /// <summary>
    /// Reads an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <param name="excludedType">The type to leave out, if any.</param>
    /// <returns>The strings the assembly's code can load.</returns>
    private static HashSet<string> Read(Assembly assembly, Type? excludedType)
    {
        HashSet<string> strings = [];

        foreach (Type type in assembly.GetTypes())
        {
            if (type == excludedType)
            {
                continue;
            }

            foreach (MethodBase method in Members(type))
            {
                foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                {
                    foreach (CustomAttributeTypedArgument argument in attribute.ConstructorArguments)
                    {
                        Collect(argument, strings);
                    }
                }

                Collect(method, strings);
            }
        }

        return strings;
    }

    /// <summary>
    /// Enumerates every method, constructor and static initializer of a type.
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
    /// <param name="strings">The set to add to.</param>
    private static void Collect(MethodBase method, HashSet<string> strings)
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
                _ = strings.Add(method.Module.ResolveString(BitConverter.ToInt32(body, i + 1)));
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
    /// <param name="strings">The set to add to.</param>
    private static void Collect(CustomAttributeTypedArgument argument, HashSet<string> strings)
    {
        if (argument.Value is string text)
        {
            _ = strings.Add(text);
        }
        else if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> arguments)
        {
            foreach (CustomAttributeTypedArgument element in arguments)
            {
                Collect(element, strings);
            }
        }
    }
}
