using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies resolving a graphics device from an external adapter identity.
/// </summary>
[TestClass]
public class GraphicsDeviceAdapterResolutionTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void ResolvesTheDeviceOfAnAdapterIdentity(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        Assert.IsTrue(GraphicsDevice.TryGetDevice(new ExternalAdapterIdentity(graphicsDevice.Luid.ToInt64()), out GraphicsDevice? resolved));
        Assert.AreSame(graphicsDevice, resolved);
    }

    [TestMethod]
    public void TheNullAdapterIdentityResolvesNoDevice()
    {
        Assert.IsFalse(GraphicsDevice.TryGetDevice(default, out GraphicsDevice? resolved));
        Assert.IsNull(resolved);
    }
}
