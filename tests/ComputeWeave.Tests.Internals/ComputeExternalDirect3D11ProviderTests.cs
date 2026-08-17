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

    /// <summary>
    /// Creates a view over a COM object, handing it one reference the view owns.
    /// </summary>
    /// <param name="value">The object the view holds as its texture.</param>
    /// <param name="withBitmap">Whether the view also holds it as its bitmap.</param>
    /// <returns>The created view.</returns>
    /// <remarks>
    /// The invariants under test are about reference counting, not about the object being a texture, so any COM
    /// object serves. Using the device avoids standing up a shared texture for this.
    /// </remarks>
    private static ExternalDirect3D11TextureView CreateView(nint value, bool withBitmap)
    {
        IUnknown* unknown = (IUnknown*)value;

        _ = unknown->AddRef();

        if (!withBitmap)
        {
            return new ExternalDirect3D11TextureView((ComputeWeave.Win32.ID3D11Texture2D*)value, null);
        }

        _ = unknown->AddRef();

        return new ExternalDirect3D11TextureView(
            (ComputeWeave.Win32.ID3D11Texture2D*)value,
            (ComputeWeave.Win32.ID2D1Bitmap1*)value);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TakesNoReferenceForAnAbsentPointer(Device device)
    {
        using Direct3D11ImmediateContext context = CreateContext(device.Get());

        nint value = (nint)context.D3D11Device;

        using ExternalDirect3D11TextureView view = CreateView(value, withBitmap: false);

        uint before = GetReferenceCount(value);

        Assert.AreEqual(0, view.Bitmap);
        Assert.AreEqual(0, view.AddRefBitmap(), "AddRefBitmap returned a pointer for an absent bitmap.");
        Assert.AreEqual(before, GetReferenceCount(value), "AddRefBitmap took a reference for an absent bitmap.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void HandsOutAReferenceTheCallerOwns(Device device)
    {
        using Direct3D11ImmediateContext context = CreateContext(device.Get());

        nint value = (nint)context.D3D11Device;

        using ExternalDirect3D11TextureView view = CreateView(value, withBitmap: true);

        foreach (bool bitmap in (bool[])[false, true])
        {
            uint before = GetReferenceCount(value);
            nint acquired = bitmap ? view.AddRefBitmap() : view.AddRefTexture();

            Assert.AreEqual(value, acquired);
            Assert.AreEqual(before + 1, GetReferenceCount(value), $"bitmap={bitmap} took no reference.");

            _ = ((IUnknown*)acquired)->Release();

            Assert.AreEqual(before, GetReferenceCount(value), $"bitmap={bitmap} did not restore the count.");
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsTheViewUsableAfterTheCallerReleases(Device device)
    {
        using Direct3D11ImmediateContext context = CreateContext(device.Get());

        nint value = (nint)context.D3D11Device;

        using ExternalDirect3D11TextureView view = CreateView(value, withBitmap: false);

        _ = ((IUnknown*)view.AddRefTexture())->Release();

        // View 自身の参照は手放していないため、まだ有効である。
        Assert.AreEqual(value, view.Texture);
        Assert.AreNotEqual(0, view.AddRefTexture());
        _ = ((IUnknown*)value)->Release();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReturnsNothingAfterTheViewIsReleased(Device device)
    {
        using Direct3D11ImmediateContext context = CreateContext(device.Get());

        nint value = (nint)context.D3D11Device;

        ExternalDirect3D11TextureView view = CreateView(value, withBitmap: true);

        view.Dispose();

        Assert.AreEqual(0, view.Texture);
        Assert.AreEqual(0, view.Bitmap);
        Assert.AreEqual(0, view.AddRefTexture());
        Assert.AreEqual(0, view.AddRefBitmap());
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
