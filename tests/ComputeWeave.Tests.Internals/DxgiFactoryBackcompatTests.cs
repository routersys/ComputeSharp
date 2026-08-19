using ComputeWeave.Graphics.Helpers;
using ComputeWeave.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class DxgiFactoryBackcompatTests
{
    [TestMethod]
    public void CountsTheReferencesItHandsOut()
    {
        IDXGIFactory6* dxgiFactory6;

        DeviceHelper.IDXGIFactory4As6Backcompat.Create(null, &dxgiFactory6);

        IUnknown* unknown = (IUnknown*)dxgiFactory6;

        Assert.AreEqual(2u, unknown->AddRef());
        Assert.AreEqual(3u, unknown->AddRef());
        Assert.AreEqual(2u, unknown->Release());
        Assert.AreEqual(1u, unknown->Release());
        Assert.AreEqual(0u, unknown->Release());
    }

    [TestMethod]
    public void AnswersTheInterfacesItImplements()
    {
        IDXGIFactory6* dxgiFactory6;

        DeviceHelper.IDXGIFactory4As6Backcompat.Create(null, &dxgiFactory6);

        IUnknown* unknown = (IUnknown*)dxgiFactory6;
        void* result;

        Assert.AreEqual(S.S_OK, (int)unknown->QueryInterface(Windows.__uuidof<IUnknown>(), &result));
        Assert.AreEqual((nint)dxgiFactory6, (nint)result);
        Assert.AreEqual(1u, unknown->Release());

        Assert.AreEqual(S.S_OK, (int)unknown->QueryInterface(Windows.__uuidof<IDXGIFactory6>(), &result));
        Assert.AreEqual((nint)dxgiFactory6, (nint)result);
        Assert.AreEqual(1u, unknown->Release());

        Assert.AreEqual(E.E_NOINTERFACE, (int)unknown->QueryInterface(Windows.__uuidof<IDXGIAdapter>(), &result));
        Assert.IsTrue(result is null);

        Assert.AreEqual(0u, unknown->Release());
    }
}
