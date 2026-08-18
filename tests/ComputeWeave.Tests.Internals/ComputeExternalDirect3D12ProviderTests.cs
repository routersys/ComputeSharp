using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TerraFX.Interop.Windows;

namespace ComputeWeave.Tests.Internals;

/// <summary>
/// Verifies the Direct3D12 queue provider the library offers to hosts.
/// </summary>
[TestClass]
public unsafe class ComputeExternalDirect3D12ProviderTests
{
    /// <summary>
    /// Creates a Direct3D12 device and command queue on the adapter of a graphics device.
    /// </summary>
    /// <param name="device">The graphics device to match.</param>
    /// <returns>The created external queue.</returns>
    private static Direct3D12ExternalQueue CreateQueue(GraphicsDevice device)
    {
        return Direct3D12ExternalQueue.Create(device.Luid.ToInt64());
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

    /// <summary>
    /// Creates a view over a COM object, handing it one reference the view owns.
    /// </summary>
    /// <param name="value">The object the view holds as its resource.</param>
    /// <returns>The created view.</returns>
    /// <remarks>
    /// The invariants under test are about reference counting, not about the object being a texture, so any COM
    /// object serves. Using the device avoids standing up a shared texture for this.
    /// </remarks>
    private static ExternalDirect3D12TextureView CreateView(nint value)
    {
        IUnknown* unknown = (IUnknown*)value;

        _ = unknown->AddRef();

        return new ExternalDirect3D12TextureView((ComputeWeave.Win32.ID3D12Resource*)value);
    }

    [TestMethod]
    public void RejectsAMissingDevice()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        _ = Assert.ThrowsExactly<ArgumentException>(() => _ = new ComputeExternalDirect3D12Provider(0, 1, scheduler));
    }

    [TestMethod]
    public void RejectsAMissingQueue()
    {
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        _ = Assert.ThrowsExactly<ArgumentException>(() => _ = new ComputeExternalDirect3D12Provider(1, 0, scheduler));
    }

    [TestMethod]
    public void RejectsAMissingScheduler()
    {
        _ = Assert.ThrowsExactly<ArgumentNullException>(() => _ = new ComputeExternalDirect3D12Provider(1, 1, null!));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReadsTheAdapterIdentityFromTheDevice(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        Assert.AreEqual(graphicsDevice.Luid.ToInt64(), provider.AdapterIdentity.AdapterLuid);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void OffersOnlyTheCapabilitiesItCanHonour(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        Assert.AreEqual(
            ExternalInteropCapabilities.SharedFence |
            ExternalInteropCapabilities.SharedTexture2D |
            ExternalInteropCapabilities.SingleImmediateContextOrdering |
            ExternalInteropCapabilities.PersistentExternalViewOrdering,
            provider.Capabilities);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void HandsOutAReferenceTheCallerOwns(Device device)
    {
        using Direct3D12ExternalQueue queue = CreateQueue(device.Get());

        nint value = (nint)queue.D3D12Device;

        using ExternalDirect3D12TextureView view = CreateView(value);

        uint before = GetReferenceCount(value);
        nint acquired = view.AddRefResource();

        Assert.AreEqual(value, acquired);
        Assert.AreEqual(before + 1, GetReferenceCount(value), "AddRefResource took no reference.");

        _ = ((IUnknown*)acquired)->Release();

        Assert.AreEqual(before, GetReferenceCount(value), "Releasing the acquired reference did not restore the count.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsTheViewUsableAfterTheCallerReleases(Device device)
    {
        using Direct3D12ExternalQueue queue = CreateQueue(device.Get());

        nint value = (nint)queue.D3D12Device;

        using ExternalDirect3D12TextureView view = CreateView(value);

        _ = ((IUnknown*)view.AddRefResource())->Release();

        // View 自身の参照は手放していないため、まだ有効である。
        Assert.AreEqual(value, view.Resource);
        Assert.AreNotEqual(0, view.AddRefResource());
        _ = ((IUnknown*)value)->Release();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReturnsNothingAfterTheViewIsReleased(Device device)
    {
        using Direct3D12ExternalQueue queue = CreateQueue(device.Get());

        nint value = (nint)queue.D3D12Device;

        ExternalDirect3D12TextureView view = CreateView(value);

        view.Dispose();

        Assert.AreEqual(0, view.Resource);
        Assert.AreEqual(0, view.AddRefResource());
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesToEnqueueBeforeItIsInitialized(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        using ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => provider.EnqueueSignal(1));
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => provider.EnqueueWait(1));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void InitializesOnceThroughADomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();
        ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
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

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        nint devicePointer = (nint)queue.D3D12Device;
        nint queuePointer = (nint)queue.D3D12Queue;

        uint deviceBefore = GetReferenceCount(devicePointer);
        uint queueBefore = GetReferenceCount(queuePointer);

        ComputeExternalDirect3D12Provider provider = new(devicePointer, queuePointer, scheduler);

        Assert.IsTrue(GetReferenceCount(devicePointer) > deviceBefore, "The provider took no reference on the device.");
        Assert.IsTrue(GetReferenceCount(queuePointer) > queueBefore, "The provider took no reference on the queue.");

        provider.Dispose();

        Assert.AreEqual(deviceBefore, GetReferenceCount(devicePointer), "The provider leaked a device reference.");
        Assert.AreEqual(queueBefore, GetReferenceCount(queuePointer), "The provider leaked a queue reference.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesNothingTwice(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        nint devicePointer = (nint)queue.D3D12Device;
        uint before = GetReferenceCount(devicePointer);

        ComputeExternalDirect3D12Provider provider = new(devicePointer, (nint)queue.D3D12Queue, scheduler);

        provider.Dispose();
        provider.Dispose();

        Assert.AreEqual(before, GetReferenceCount(devicePointer), "Disposing twice released the device twice.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryInterfaceWhenConstructionFails(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        nint queuePointer = (nint)queue.D3D12Queue;

        uint before = GetReferenceCount(queuePointer);

        // キューをデバイスとして渡す。ID3D12Device への照会が失敗し、構築が中断する。
        // 不変条件が定めるのは参照の均衡であり例外の種別ではないため、種別を固定しない。
        bool threw = false;

        try
        {
            _ = new ComputeExternalDirect3D12Provider(queuePointer, queuePointer, scheduler);
        }
        catch (Exception)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "A construction that cannot query its interfaces was accepted.");
        Assert.AreEqual(before, GetReferenceCount(queuePointer), "A failed construction leaked a reference.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RefusesASecondInitialization(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        // 登録が一度目の Initialize を行う。二度目は拒まれる。
        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        // ref struct はラムダへ捕捉できないため、その場で構築して渡す。
        try
        {
            provider.Initialize(default);

            Assert.Fail("The second initialization was accepted.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void EnqueuesWithoutManagedAllocation(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        long minimum = long.MaxValue;
        ulong value = 1;

        for (int i = 0; i < 10; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int j = 0; j < 100; j++)
            {
                provider.EnqueueSignal(value);
                provider.EnqueueWait(value);
                provider.FlushAfterSignal();
                value++;
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.AreEqual(0, minimum, "The enqueue path allocates managed memory.");
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LeavesTheSchedulerToItsOwner(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using Direct3D12ExternalQueue queue = CreateQueue(graphicsDevice);
        using ComputeExternalQueueScheduler scheduler = ComputeExternalQueueScheduler.Create();

        ComputeExternalDirect3D12Provider provider = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        provider.Dispose();

        // 破棄済みでない Scheduler は登録を受け付ける。ここで登録が通ることが、Provider が破棄していない証拠になる。
        ComputeExternalDirect3D12Provider second = new(
            (nint)queue.D3D12Device,
            (nint)queue.D3D12Queue,
            scheduler);

        using ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(second);

        Assert.AreNotEqual(0ul, domain.Id.Value);
    }
}
