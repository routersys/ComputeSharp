using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class DeviceLostCallbackHandleTests
{
    private const string Unregistration = "GraphicsDevice.UnregisterDeviceLostCallback";

    private const string Callback = "GraphicsDevice.WaitForSingleObjectCallbackForRegisterDeviceLostCallback";

    private const string Unregister = "Windows.UnregisterWait";

    private const string Free = "GCHandle.Free";

    [TestMethod]
    public void ReleasesTheDeviceLostHandleFromItsCallback()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.AreNotEqual(0, graph.GetCallees(Callback).Count, $"{Callback} was not found in the assembly");
        Assert.IsTrue(
            graph.GetCallees(Callback).Contains(Free),
            "the device lost callback no longer releases the handle it owns");
    }

    [TestMethod]
    public void LeavesTheDeviceLostHandleToItsCallback()
    {
        AssemblyCallGraph graph = AssemblyCallGraph.Read();

        Assert.IsTrue(
            graph.GetCallees(Unregistration).Contains(Unregister),
            $"{Unregistration} no longer cancels the wait this test looks for");
        Assert.IsFalse(
            graph.GetCallees(Unregistration).Contains(Free),
            "the disposal path releases a handle the callback also releases");
    }
}
