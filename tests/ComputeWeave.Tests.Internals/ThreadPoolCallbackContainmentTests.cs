using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ThreadPoolCallbackContainmentTests
{
    private const string FenceCallback = "GraphicsDevice.WaitForSingleObjectCallbackForWaitForFenceAsync";

    private const string Deliver = "WaitForFenceValueTaskSource.Fail";

    [TestMethod]
    public void TheFenceWaitCallbackCatchesEveryManagedException()
    {
        IList<ExceptionHandlingClause> clauses = GetClauses("WaitForSingleObjectCallbackForWaitForFenceAsync");

        Assert.IsTrue(
            clauses.Any(IsFullContainmentClause),
            "the fence wait callback lets a managed exception reach its unmanaged caller");
    }

    [TestMethod]
    public void TheFenceWaitCallbackReleasesItsNativeStateOnEveryPath()
    {
        IList<ExceptionHandlingClause> clauses = GetClauses("WaitForSingleObjectCallbackForWaitForFenceAsync");

        Assert.IsTrue(
            clauses.Any(static clause => clause.Flags == ExceptionHandlingClauseOptions.Finally),
            "the fence wait callback releases its wait registration, event and context only when nothing throws");
    }

    [TestMethod]
    public void TheFenceWaitCallbackDeliversItsFailureToTheAwaiter()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(FenceCallback).Count, $"{FenceCallback} was not found in the assembly");
        Assert.IsTrue(
            graph.GetCallees(FenceCallback).Contains(Deliver),
            "the fence wait callback drops the failure instead of delivering it to the awaiter");
    }

    [TestMethod]
    public void TheDeviceLostCallbackCatchesEveryManagedException()
    {
        IList<ExceptionHandlingClause> clauses = GetClauses("WaitForSingleObjectCallbackForRegisterDeviceLostCallback");

        Assert.IsTrue(
            clauses.Any(IsFullContainmentClause),
            "the device lost callback lets a managed exception reach its unmanaged caller");
    }

    [TestMethod]
    public void TheDeviceLostCallbackReleasesItsHandleOnEveryPath()
    {
        IList<ExceptionHandlingClause> clauses = GetClauses("WaitForSingleObjectCallbackForRegisterDeviceLostCallback");

        Assert.IsTrue(
            clauses.Any(static clause => clause.Flags == ExceptionHandlingClauseOptions.Finally),
            "the device lost callback releases its handle only when nothing throws");
    }

    [TestMethod]
    public void TheContainmentPredicateAcceptsABareCatch()
    {
        MethodInfo? method = typeof(ContainmentPredicateProbes).GetMethod(nameof(ContainmentPredicateProbes.HasBareCatch), BindingFlags.Static | BindingFlags.Public);

        Assert.IsNotNull(method, $"{nameof(ContainmentPredicateProbes.HasBareCatch)} was not found");

        IList<ExceptionHandlingClause> clauses = method.GetMethodBody()!.ExceptionHandlingClauses;

        Assert.IsTrue(
            clauses.Any(IsFullContainmentClause),
            "a bare catch catches every managed exception and must count as full containment");
    }

    [TestMethod]
    public void TheContainmentPredicateRejectsANarrowCatch()
    {
        MethodInfo? method = typeof(ContainmentPredicateProbes).GetMethod(nameof(ContainmentPredicateProbes.HasNarrowCatch), BindingFlags.Static | BindingFlags.Public);

        Assert.IsNotNull(method, $"{nameof(ContainmentPredicateProbes.HasNarrowCatch)} was not found");

        IList<ExceptionHandlingClause> clauses = method.GetMethodBody()!.ExceptionHandlingClauses;

        Assert.IsFalse(
            clauses.Any(IsFullContainmentClause),
            "a catch narrower than System.Exception lets other managed exceptions through and must not count as full containment");
    }

    [TestMethod]
    public void TheInspectionReportsNoHandlerForAMethodThatHasNone()
    {
        MethodInfo? method = typeof(GraphicsDevice)
            .GetNestedType("WaitForFenceValueTaskSource", BindingFlags.NonPublic)?
            .GetMethod("Complete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(method, "WaitForFenceValueTaskSource.Complete was not found");
        Assert.AreEqual(0, method.GetMethodBody()!.ExceptionHandlingClauses.Count, "the inspection reports a handler where the method has none");
    }

    private static IList<ExceptionHandlingClause> GetClauses(string name)
    {
        MethodInfo? method = typeof(GraphicsDevice).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(method, $"{name} was not found on GraphicsDevice");

        return method.GetMethodBody()!.ExceptionHandlingClauses;
    }

    /// <summary>
    /// Determines whether a clause catches every managed exception that can reach it.
    /// </summary>
    /// <remarks>
    /// A bare <c>catch { }</c> catches <see cref="object"/> at the IL level, which is broader than
    /// <see cref="Exception"/>, not narrower. Comparing <see cref="ExceptionHandlingClause.CatchType"/>
    /// only against <see cref="Exception"/> misses this and reports full containment as missing.
    /// </remarks>
    private static bool IsFullContainmentClause(ExceptionHandlingClause clause)
    {
        return clause.Flags == ExceptionHandlingClauseOptions.Clause &&
            (clause.CatchType == typeof(Exception) || clause.CatchType == typeof(object));
    }

    /// <summary>
    /// Method bodies used to verify that <see cref="IsFullContainmentClause"/> classifies containment correctly.
    /// </summary>
    private static class ContainmentPredicateProbes
    {
        public static int Sink;

        public static void HasBareCatch()
        {
            try
            {
                Sink = 1;
            }
            catch
            {
                Sink = 2;
            }
        }

        public static void HasNarrowCatch()
        {
            try
            {
                Sink = 1;
            }
            catch (InvalidOperationException)
            {
                Sink = 2;
            }
        }
    }
}
