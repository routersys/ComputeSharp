using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class FenceWaitRegistrationTests
{
    private const string Registration = "GraphicsDevice.WaitForFenceAsync";

    private const string Callback = "GraphicsDevice.WaitForSingleObjectCallbackForWaitForFenceAsync";

    private const string Register = "Windows.RegisterWaitForSingleObject";

    private const string Unregister = "Windows.UnregisterWait";

    [TestMethod]
    public void RegistersTheFenceWaitFromTheAsynchronousSubmission()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(Registration).Count, $"{Registration} was not found in the assembly");
        Assert.IsTrue(
            graph.GetCallees(Registration).Contains(Register),
            $"{Registration} no longer registers the wait this test looks for");
    }

    [TestMethod]
    public void ReleasesTheFenceWaitRegistrationFromItsCallback()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(Callback).Count, $"{Callback} was not found in the assembly");
        Assert.IsTrue(
            graph.GetCallees(Callback).Contains(Unregister),
            "the completion callback leaves its wait registration behind");
    }

    [TestMethod]
    public void ReleasesTheFenceWaitRegistrationWhenTheEventCannotBeArmed()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(Registration).Count, $"{Registration} was not found in the assembly");
        Assert.IsTrue(
            graph.GetCallees(Registration).Contains(Unregister),
            "the registration is left behind when the fence event cannot be armed");
    }
}
