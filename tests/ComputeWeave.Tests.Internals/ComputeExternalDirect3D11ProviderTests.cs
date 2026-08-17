using System;
using System.Reflection;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.Windows;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the Direct3D11 provider the library offers to hosts.
/// </summary>
[TestClass]
public unsafe class ComputeExternalDirect3D11ProviderTests
{
    /// <summary>
    /// Creates a Direct3D11 immediate context on the adapter of a graphics device.
    /// </summary>
    /// <param name="device">The graphics device to match.</param>
    /// <returns>The created immediate context.</returns>
    private static Direct3D11ImmediateContext CreateContext(GraphicsDevice device)
    {
        return Direct3D11ImmediateContext.Create(device.Luid.ToInt64());
    }

    /// <summary>
    /// Reads the reference count of a COM object without keeping a reference.
    /// </summary>
    /// <param name="value">The object to read.</param>
    /// <returns>The reference count after the read completes.</returns>
    private static uint GetReferenceCount(nint value)
    {
        IUnknown* unknown = (IUnknown*)value;

        _ = unknown->AddRef();

        return unknown->Release();
    }

    [TestMethod]
    public void RejectsAMissingDevice()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        _ = Assert.ThrowsExactly<ArgumentException>(() => _ = new ComputeExternalDirect3D11Provider(0, 1, 0, scheduler));
    }

    [TestMethod]
    public void RejectsAMissingImmediateContext()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        _ = Assert.ThrowsExactly<ArgumentException>(() => _ = new ComputeExternalDirect3D11Provider(1, 0, 0, scheduler));
    }

    [TestMethod]
    public void RejectsAMissingScheduler()
    {
        _ = Assert.ThrowsExactly<ArgumentNullException>(() => _ = new ComputeExternalDirect3D11Provider(1, 1, 0, null!));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReadsTheAdapterIdentityFromTheDevice(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D11Provider provider = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        Assert.AreEqual(graphicsDevice.Luid.ToInt64(), provider.AdapterIdentity.AdapterLuid);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OffersOnlyTheCapabilitiesItCanHonour(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D11Provider provider = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        Assert.AreEqual(
            ExternalInteropCapabilities.SharedFence |
            ExternalInteropCapabilities.SharedTexture2D |
            ExternalInteropCapabilities.SingleImmediateContextOrdering |
            ExternalInteropCapabilities.PersistentExternalViewOrdering,
            provider.Capabilities);
    }

    [TestMethod]
    public void NeverForbidsDrawingFromAView()
    {
        Type type = typeof(ComputeExternalDirect3D11Provider);
        MethodInfo? method = type.GetMethod("GetBitmapOptions", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "GetBitmapOptions is missing.");

        // 3 は TARGET | CANNOT_DRAW にあたる。Sampled の View が描画元に使えなくなる。
        foreach (ExternalTextureUsage usage in Enum.GetValues<ExternalTextureUsage>())
        {
            uint options = Convert.ToUInt32(method.Invoke(null, [usage]));

            Assert.AreEqual(0u, options & 2u, $"The bitmap of a {usage} view forbids drawing from it.");
        }
    }

    [TestMethod]
    public void MarksOnlyARenderTargetViewAsATarget()
    {
        Type type = typeof(ComputeExternalDirect3D11Provider);
        MethodInfo method = type.GetMethod("GetBitmapOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.AreEqual(1u, Convert.ToUInt32(method.Invoke(null, [ExternalTextureUsage.RenderTarget])));
        Assert.AreEqual(0u, Convert.ToUInt32(method.Invoke(null, [ExternalTextureUsage.Sampled])));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesToEnqueueBeforeItIsInitialized(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D11Provider provider = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => provider.EnqueueSignal(1));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => provider.EnqueueWait(1));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void InitializesOnceThroughADomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        ComputeExternalDirect3D11Provider provider = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        Assert.AreNotEqual(0ul, domain.Id.Value);
        Assert.AreEqual(provider.Capabilities, domain.Capabilities);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryInterfaceItQueried(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        nint devicePointer = (nint)context.D3D11Device;
        nint contextPointer = (nint)context.D3D11ImmediateContext;

        uint deviceBefore = GetReferenceCount(devicePointer);
        uint contextBefore = GetReferenceCount(contextPointer);

        ComputeExternalDirect3D11Provider provider = new(devicePointer, contextPointer, 0, scheduler);

        Assert.IsTrue(GetReferenceCount(devicePointer) > deviceBefore, "The provider took no reference on the device.");
        Assert.IsTrue(GetReferenceCount(contextPointer) > contextBefore, "The provider took no reference on the context.");

        provider.Dispose();

        Assert.AreEqual(deviceBefore, GetReferenceCount(devicePointer), "The provider leaked a device reference.");
        Assert.AreEqual(contextBefore, GetReferenceCount(contextPointer), "The provider leaked a context reference.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesNothingTwice(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        nint devicePointer = (nint)context.D3D11Device;
        uint before = GetReferenceCount(devicePointer);

        ComputeExternalDirect3D11Provider provider = new(devicePointer, (nint)context.D3D11ImmediateContext, 0, scheduler);

        provider.Dispose();
        provider.Dispose();

        Assert.AreEqual(before, GetReferenceCount(devicePointer), "Disposing twice released the device twice.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LeavesTheSchedulerToItsOwner(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D11ImmediateContext context = CreateContext(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        ComputeExternalDirect3D11Provider provider = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        provider.Dispose();

        // 破棄済みの Scheduler は登録を拒む。ここで登録が通ることが、Provider が破棄していない証拠になる。
        ComputeExternalDirect3D11Provider second = new(
            (nint)context.D3D11Device,
            (nint)context.D3D11ImmediateContext,
            0,
            scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(second);

        Assert.AreNotEqual(0ul, domain.Id.Value);
    }
}
