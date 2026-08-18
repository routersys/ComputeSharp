using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the diagnostic identifiers wired into the throw sites that terminate a device.
/// </summary>
/// <remarks>
/// The terminal queue failures cannot be injected at runtime without a real queue failure, so the wiring is
/// verified from the IL instead: the identifier constants are inlined as string literals at each site, and a
/// site that loses or swaps its literal no longer resolves the expected string. The expected values are
/// written here as literals, taken from section 19.2 of the pipeline interop specification.
/// </remarks>
[TestClass]
public class TerminalDiagnosticIdTests
{
    /// <summary>
    /// The string literals each terminal site must load.
    /// </summary>
    private static readonly (Type Type, string Method, string[] Ids)[] Sites =
    [
        (typeof(GraphicsDevice), "ExecutePipelineCommandLists", ["CMPW5001", "CMPW5002"]),
        (typeof(GraphicsDevice), "ExecuteCopyCommandLists", ["CMPW5001", "CMPW5002"]),
        (typeof(GraphicsDevice), "EnqueueInteropFinalDrain", ["CMPW5002"]),
        (typeof(GraphicsDevice), "ArmComputeFenceEvent", ["CMPW5001"]),
        (typeof(GraphicsDevice), "WaitForComputeFenceValue", ["CMPW5001"]),
        (typeof(GraphicsDevice), "WaitForSubmission", ["CMPW5001"]),
        (typeof(GraphicsDevice), "ThrowTerminalSequenceExhaustion", ["CMPW5004"])
    ];

    /// <summary>
    /// Collects every string a method body can resolve from an inline <c>ldstr</c> operand.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <returns>The resolvable strings.</returns>
    /// <remarks>
    /// The scan tries every byte offset, so it can pick up coincidental values next to the real operands.
    /// That only ever adds strings, so asserting that an expected literal is present stays sound.
    /// </remarks>
    private static HashSet<string> GetResolvableStrings(MethodInfo method)
    {
        byte[] body = method.GetMethodBody()!.GetILAsByteArray()!;
        Module module = method.Module;
        HashSet<string> strings = [];

        for (int i = 0; i < body.Length - 4; i++)
        {
            if (body[i] != 0x72)
            {
                continue;
            }

            int token = BitConverter.ToInt32(body, i + 1);

            try
            {
                _ = strings.Add(module.ResolveString(token));
            }
            catch (ArgumentException)
            {
            }
        }

        return strings;
    }

    [TestMethod]
    public void EveryTerminalSiteLoadsItsDiagnosticId()
    {
        foreach ((Type type, string name, string[] ids) in Sites)
        {
            MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, $"{type.Name}.{name} is missing.");

            HashSet<string> strings = GetResolvableStrings(method);

            foreach (string id in ids)
            {
                Assert.IsTrue(strings.Contains(id), $"{type.Name}.{name} does not load \"{id}\".");
            }
        }
    }

    [TestMethod]
    public void TheInteropExecutorTellsTheTwoTerminalReasonsApart()
    {
        Type? type = typeof(GraphicsDevice).Assembly.GetType("ComputeWeave.Graphics.Pipelines.ComputeSubmissionExecutor");

        Assert.IsNotNull(type, "ComputeSubmissionExecutor is missing.");

        bool found5001 = false;
        bool found5002 = false;

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (method.GetMethodBody() is null)
            {
                continue;
            }

            HashSet<string> strings = GetResolvableStrings(method);

            found5001 |= strings.Contains("CMPW5001");
            found5002 |= strings.Contains("CMPW5002");
        }

        Assert.IsTrue(found5001, "No interop submission site loads \"CMPW5001\".");
        Assert.IsTrue(found5002, "No interop submission site loads \"CMPW5002\".");
    }
}
