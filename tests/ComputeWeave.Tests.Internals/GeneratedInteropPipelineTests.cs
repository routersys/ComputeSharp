using System;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable IDE0051

namespace ComputeWeave.Tests.Internals;

[ComputePipelineHost("device", 1)]
internal sealed partial class GeneratedInteropPipelineHost
{
    private readonly GraphicsDevice device;

    [ComputePipeline]
    [ComputeInterop]
    private void Blit(
        in ComputeContext context,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> target)
    {
        _ = this.device;

        context.Clear(target);
    }
}

[TestClass]
public class GeneratedInteropPipelineTests
{
    private sealed class Fixture(GraphicsDevice device) : IDisposable
    {
        public GraphicsDevice Device { get; } = device;

        public FakeInteropScheduler Scheduler { get; } = new();

        public FakeInteropProvider Provider { get; private set; } = null!;

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public GeneratedInteropResources Resources { get; private set; } = null!;

        public GeneratedInteropPipelineHost Host { get; private set; } = null!;

        public Fixture Register()
        {
            Provider = new FakeInteropProvider(Device, Scheduler);
            Domain = Device.RegisterExternalDomain(Provider);
            Resources = GeneratedInteropResources.Create(Device, Domain);
            Host = GeneratedInteropPipelineHost.Create(Device, 3);

            Assert.IsTrue(Resources.TryEnsureSource(16, 16, out _));

            return this;
        }

        public ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> Binding => Resources.GetSourceComputeBinding();

        public ReadWriteTexture2D<Bgra32, Float4> Target => Binding.Resource!;

        public ExternalOwnershipState Ownership => GetOwner(Target).GetResourceRecord(0).ReadOwnership();

        public void Dispose()
        {
            Host.Dispose();
            Host.WaitForDisposal();
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

    private static ResourceGenerationOwner GetOwner(ReadWriteTexture2D<Bgra32, Float4> texture)
    {
        Assert.IsTrue(((IGenerationBoundResource)texture).TryGetGenerationBinding(out ResourceUsageBinding binding));

        return (ResourceGenerationOwner)binding.Set.Owner;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void SubmitsAnInteropRoundTripThroughTheGeneratedWrapper(Device device)
    {
        using Fixture fixture = Create(device);

        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Ownership);

        ComputeSubmission submission = fixture.Host.Blit(fixture.Binding);

        Assert.AreEqual(1, fixture.Provider.SignalCount);
        Assert.AreEqual(1, fixture.Provider.FlushCount);
        Assert.AreEqual(1, fixture.Provider.WaitCount);
        Assert.IsTrue(fixture.Provider.ObservedSignalValue < fixture.Provider.ObservedWaitValue);
        Assert.IsFalse(fixture.Scheduler.IsReserved);

        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Ownership);

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquiresAndReleasesTheSharedTextureOnEverySubmission(Device device)
    {
        using Fixture fixture = Create(device);

        for (int i = 1; i <= 3; i++)
        {
            ComputeSubmission submission = fixture.Host.Blit(fixture.Binding);

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
            Assert.AreEqual(i, fixture.Provider.SignalCount);
            Assert.AreEqual(i, fixture.Provider.WaitCount);
            Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Ownership);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ClearsTheSharedTextureThroughTheRecordedBody(Device device)
    {
        using Fixture fixture = Create(device);

        ReadWriteTexture2D<Bgra32, Float4> target = fixture.Target;

        target.CopyFrom(CreateOpaquePixels(target.Width * target.Height));

        ComputeSubmission submission = fixture.Host.Blit(fixture.Binding);

        submission.Wait();

        foreach (Bgra32 pixel in target.ToArray())
        {
            Assert.AreEqual(0, pixel.R + pixel.G + pixel.B + pixel.A);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsABindingWhoseGenerationWasReplaced(Device device)
    {
        using Fixture fixture = Create(device);

        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> binding = fixture.Binding;

        Assert.IsTrue(fixture.Resources.TryEnsureSource(32, 32, out bool changed));
        Assert.IsTrue(changed);

        int signalCount = fixture.Provider.SignalCount;
        int waitCount = fixture.Provider.WaitCount;

        _ = Assert.ThrowsException<InvalidOperationException>(() => fixture.Host.Blit(binding));

        Assert.AreEqual(signalCount, fixture.Provider.SignalCount);
        Assert.AreEqual(waitCount, fixture.Provider.WaitCount);
        Assert.IsNull(fixture.Domain.PoisonReason);

        ComputeSubmission submission = fixture.Host.Blit(fixture.Binding);

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TearsDownTheDomainWhenTheProviderCannotSignal(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            FakeExternalView view = fixture.Provider.LastOpenedView!;
            ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> binding = fixture.Binding;
            ResourceGenerationOwner owner = GetOwner(binding.Resource!);

            fixture.Provider.ThrowOnSignal = true;

            _ = Assert.ThrowsException<InvalidOperationException>(() => fixture.Host.Blit(binding));

            Assert.IsNotNull(fixture.Domain.PoisonReason);
            Assert.IsTrue(fixture.Domain.IsDisposeRequested);
            Assert.AreEqual(ExternalOwnershipState.Faulted, owner.GetResourceRecord(0).ReadOwnership());

            fixture.Resources.WaitForDisposal();

            Assert.IsFalse(fixture.Scheduler.IsReserved);
            Assert.AreEqual(1, view.DisposeCount);
            Assert.AreEqual(0, fixture.Provider.WaitCount);
            Assert.AreEqual(ResourceGenerationState.Released, owner.GetResourceRecord(0).ReadLifecycle());
            Assert.IsTrue(fixture.Domain.IsDisposed);
            Assert.AreEqual(1, fixture.Provider.DisposeCount);
        }
        finally
        {
            fixture.Host.Dispose();
            fixture.Host.WaitForDisposal();
            fixture.Resources.Dispose();
            fixture.Scheduler.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesTheSubmissionWhenTheProviderCannotWait(Device device)
    {
        Fixture fixture = Create(device);

        try
        {
            FakeExternalView view = fixture.Provider.LastOpenedView!;
            ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> binding = fixture.Binding;
            ResourceGenerationOwner owner = GetOwner(binding.Resource!);

            fixture.Provider.ThrowOnWait = true;

            _ = Assert.ThrowsException<InvalidOperationException>(() => fixture.Host.Blit(binding));

            Assert.AreEqual(1, fixture.Provider.SignalCount);
            Assert.AreEqual(1, fixture.Provider.WaitCount);
            Assert.IsFalse(fixture.Device.IsDeviceTerminal);
            Assert.IsNotNull(fixture.Domain.PoisonReason);
            Assert.IsTrue(fixture.Domain.IsDisposeRequested);
            Assert.AreEqual(ExternalOwnershipState.Faulted, owner.GetResourceRecord(0).ReadOwnership());

            fixture.Resources.WaitForDisposal();

            Assert.IsFalse(fixture.Scheduler.IsReserved);
            Assert.AreEqual(1, view.DisposeCount);
            Assert.AreEqual(ResourceGenerationState.Released, owner.GetResourceRecord(0).ReadLifecycle());
            Assert.IsTrue(fixture.Domain.IsDisposed);
            Assert.AreEqual(1, fixture.Provider.DisposeCount);
        }
        finally
        {
            fixture.Host.Dispose();
            fixture.Host.WaitForDisposal();
            fixture.Resources.Dispose();
            fixture.Scheduler.Dispose();
        }
    }

    private static Bgra32[] CreateOpaquePixels(int count)
    {
        Bgra32[] pixels = new Bgra32[count];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Bgra32(255, 255, 255, 255);
        }

        return pixels;
    }
}
