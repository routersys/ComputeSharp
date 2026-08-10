using System;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[ComputeInteropResourceSet]
internal sealed partial class GeneratedInteropResources
{
    [ComputeSharedTexture(
        ComputeResourceResizePolicy.Exact,
        ComputeResourceAccess.ReadWrite,
        ExternalResourceAccess.Write,
        ExternalTextureUsage.RenderTarget,
        ComputeAlphaMode.Premultiplied,
        ComputeSharedTextureInitialOwner.External,
        ComputeResourceRecovery.RecreateFromHost)]
    private readonly SharedTextureSlot<Bgra32, Float4, FakeExternalView> source;

    [ComputeSharedTexture(
        ComputeResourceResizePolicy.GrowOnly,
        ComputeResourceAccess.ReadWrite,
        ExternalResourceAccess.Read,
        ExternalTextureUsage.Sampled,
        ComputeAlphaMode.Premultiplied,
        ComputeSharedTextureInitialOwner.Compute,
        ComputeResourceRecovery.Recompute)]
    private readonly SharedTextureSlot<Bgra32, Float4, FakeExternalView> output;
}

[TestClass]
public class GeneratedInteropResourceSetTests
{
    private sealed class Fixture(GraphicsDevice device) : IDisposable
    {
        public FakeInteropScheduler Scheduler { get; } = new();

        public FakeInteropProvider Provider { get; private set; } = null!;

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public GeneratedInteropResources Resources { get; private set; } = null!;

        public Fixture Register()
        {
            Provider = new FakeInteropProvider(device, Scheduler);
            Domain = device.RegisterExternalDomain(Provider);
            Resources = GeneratedInteropResources.Create(device, Domain);

            return this;
        }

        public void Dispose()
        {
            Resources.Dispose();
            Resources.WaitForDisposal();
            Domain.Dispose();
            Scheduler.Dispose();
        }
    }

    private static Fixture Create(Device device)
    {
        return new Fixture(device.Get()).Register();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesEveryDeclaredSharedTexture(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsFalse(fixture.Resources.GetSourceComputeBinding().IsValid);
        Assert.IsFalse(fixture.Resources.GetOutputComputeBinding().IsValid);

        Assert.IsTrue(fixture.Resources.TryEnsureSource(64, 32, out bool changed));
        Assert.IsTrue(changed);

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(16, 8, out changed));
        Assert.IsTrue(changed);

        Assert.AreEqual(2, fixture.Provider.OpenSharedTextureCount);

        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> source = fixture.Resources.GetSourceComputeBinding();

        Assert.IsTrue(source.IsValid);
        Assert.AreEqual(64, source.Resource!.Width);
        Assert.AreEqual(32, source.Resource.Height);

        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> output = fixture.Resources.GetOutputComputeBinding();

        Assert.IsTrue(output.IsValid);
        Assert.AreEqual(16, output.Resource!.Width);
        Assert.AreEqual(8, output.Resource.Height);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsTheAllocatedSizeOfAGrowOnlySharedTexture(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsFalse(fixture.Resources.TryGetOutputAllocatedSize(out int width, out int height));
        Assert.AreEqual(0, width);
        Assert.AreEqual(0, height);

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(64, 32, out bool changed));
        Assert.IsTrue(changed);

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(32, 16, out changed));
        Assert.IsFalse(changed);

        Assert.IsTrue(fixture.Resources.TryGetOutputAllocatedSize(out width, out height));
        Assert.AreEqual(64, width);
        Assert.AreEqual(32, height);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void GetsTheAllocatedSizeWithoutManagedAllocation(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(64, 32, out _));
        Assert.IsTrue(fixture.Resources.TryGetOutputAllocatedSize(out _, out _));

        long minimum = long.MaxValue;
        bool result = false;
        int width = 0;
        int height = 0;

        for (int i = 0; i < 10; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int j = 0; j < 1000; j++)
            {
                result = fixture.Resources.TryGetOutputAllocatedSize(out width, out height);
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.IsTrue(result);
        Assert.AreEqual(64, width);
        Assert.AreEqual(32, height);
        Assert.AreEqual(0, minimum);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void BindsEachSharedTextureToTheContractOfItsOwnField(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(8, 8, out _));

        Assert.AreEqual(ExternalTextureUsage.Sampled, fixture.Provider.ObservedTextureDescriptor.ExternalUsage);

        Assert.IsTrue(fixture.Resources.TryEnsureSource(8, 8, out _));

        Assert.AreEqual(ExternalTextureUsage.RenderTarget, fixture.Provider.ObservedTextureDescriptor.ExternalUsage);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void LendsTheExternalViewOfEverySharedTextureTheExternalQueueOwns(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.IsTrue(fixture.Resources.TryEnsureSource(8, 8, out _));

        FakeExternalView source = fixture.Provider.LastOpenedView!;

        using (BorrowedExternalTextureView<FakeExternalView> borrow = fixture.Resources.BeginSourceExternalOperation())
        {
            Assert.IsTrue(borrow.IsValid);
            Assert.AreSame(source, borrow.DangerousGetView());
        }

        using ExternalTextureLease<FakeExternalView> lease = fixture.Resources.AcquireSourceExternalViewLease();

        Assert.AreSame(source, lease.DangerousGetView());

        Assert.IsTrue(fixture.Resources.TryEnsureOutput(8, 8, out _));

        _ = Assert.ThrowsException<InvalidOperationException>(fixture.Resources.AcquireOutputExternalViewLease);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryExternalViewOnDispose(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            Assert.IsTrue(fixture.Resources.TryEnsureSource(16, 16, out _));

            FakeExternalView source = fixture.Provider.LastOpenedView!;

            Assert.IsTrue(fixture.Resources.TryEnsureOutput(16, 16, out _));

            FakeExternalView output = fixture.Provider.LastOpenedView!;

            Assert.AreNotSame(source, output);

            fixture.Resources.Dispose();
            fixture.Resources.WaitForDisposal();

            Assert.AreEqual(1, source.DisposeCount);
            Assert.AreEqual(1, output.DisposeCount);
            Assert.IsFalse(fixture.Resources.GetSourceComputeBinding().IsValid);
        }
        finally
        {
            fixture.Domain.Dispose();
            fixture.Scheduler.Dispose();
        }
    }
}
