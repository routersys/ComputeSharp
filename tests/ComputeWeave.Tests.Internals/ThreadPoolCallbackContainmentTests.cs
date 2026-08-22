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
            clauses.Any(static clause => clause.Flags == ExceptionHandlingClauseOptions.Clause && clause.CatchType == typeof(Exception)),
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
            clauses.Any(static clause => clause.Flags == ExceptionHandlingClauseOptions.Clause && clause.CatchType == typeof(Exception)),
            "the device lost callback lets a managed exception reach its unmanaged caller");
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
}
