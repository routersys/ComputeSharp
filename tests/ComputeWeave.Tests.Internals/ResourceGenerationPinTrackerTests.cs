using System;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using ComputeWeave.Tests.Internals.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public unsafe class ResourceGenerationPinTrackerTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void PinsEveryBoundGenerationAndReleasesThemOnRollback(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 2);

        using ReadWriteBuffer<int> first = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> second = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));

            ref RecordingBundleEntry bundle = ref host.RecordingBundles.GetBundle(bundleIndex);

            Assert.IsTrue(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, first));
            Assert.IsTrue(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, second));

            Assert.AreEqual(2, bundle.Count);
            Assert.AreEqual(1, RecordingReferenceCount(first));
            Assert.AreEqual(1, RecordingReferenceCount(second));

            ResourceGenerationPinTracker.Rollback(graphicsDevice, host.RecordingBundles.Storage, ref bundle);

            Assert.AreEqual(0, bundle.Count);
            Assert.AreEqual(0, RecordingReferenceCount(first));
            Assert.AreEqual(0, RecordingReferenceCount(second));

            host.RecordingBundles.Return(bundleIndex);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ConvertsObservedGenerationsAndReleasesUnobservedOnes(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 2);

        using ReadWriteBuffer<int> observed = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> unobserved = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));

            ref RecordingBundleEntry bundle = ref host.RecordingBundles.GetBundle(bundleIndex);

            Assert.IsTrue(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, observed));
            Assert.IsTrue(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, unobserved));

            Assert.IsTrue(((IGenerationBoundResource)observed).TryGetGenerationBinding(out ResourceUsageBinding binding));

            GraphicsResourceUsageEntry[] usages =
            [
                new GraphicsResourceUsageEntry
                {
                    Set = binding.Set,
                    ResourceIndex = binding.ResourceIndex,
                    Generation = binding.Generation,
                    Access = ComputeResourceAccess.ReadWrite,
                    FirstState = binding.ResidentState,
                    FinalState = binding.ResidentState
                }
            ];

            ResourceGenerationPinTracker.ConvertToPendingSubmission(graphicsDevice, host.RecordingBundles.Storage, ref bundle, usages);

            Assert.AreEqual(0, bundle.Count);

            Assert.AreEqual(0, RecordingReferenceCount(observed));
            Assert.AreEqual(1, PendingSubmissionReferenceCount(observed));

            Assert.AreEqual(0, RecordingReferenceCount(unobserved));
            Assert.AreEqual(0, PendingSubmissionReferenceCount(unobserved));

            ((IResourceGenerationOwner)observed).GetResourceRecord(0).ReleasePendingSubmissionReference();

            host.RecordingBundles.Return(bundleIndex);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesAPinThatDoesNotFitTheRecordingBundle(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteBuffer<int> first = graphicsDevice.AllocateReadWriteBuffer<int>(64);
        using ReadWriteBuffer<int> second = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            Assert.AreEqual(1, host.RecordingBundles.PinCapacity);
            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));

            ref RecordingBundleEntry bundle = ref host.RecordingBundles.GetBundle(bundleIndex);

            Assert.IsTrue(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, first));
            Assert.IsFalse(ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref bundle, second));

            Assert.AreEqual(1, bundle.Count);
            Assert.AreEqual(1, RecordingReferenceCount(first));
            Assert.AreEqual(0, RecordingReferenceCount(second));

            ResourceGenerationPinTracker.Rollback(graphicsDevice, host.RecordingBundles.Storage, ref bundle);

            host.RecordingBundles.Return(bundleIndex);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsANullResource(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        try
        {
            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));

            ref RecordingBundleEntry bundle = ref host.RecordingBundles.GetBundle(bundleIndex);

            _ = Assert.ThrowsExactly<ArgumentNullException>(
                () => ResourceGenerationPinTracker.TryPin(graphicsDevice, host.RecordingBundles.Storage, ref host.RecordingBundles.GetBundle(0), null!));

            Assert.AreEqual(0, bundle.Count);

            host.RecordingBundles.Return(bundleIndex);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RentsEveryDeclaredRecordingBundleOnce(Device device)
    {
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        try
        {
            Assert.AreEqual(1, host.RecordingBundles.Capacity);
            Assert.AreEqual(1, host.RecordingBundles.AvailableCount);

            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));
            Assert.IsFalse(host.RecordingBundles.TryRent(out int exhausted));

            Assert.AreEqual(-1, exhausted);
            Assert.AreEqual(0, host.RecordingBundles.AvailableCount);

            host.RecordingBundles.Return(bundleIndex);

            Assert.AreEqual(1, host.RecordingBundles.AvailableCount);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => host.RecordingBundles.Return(bundleIndex));
        }
        finally
        {
            registry.Dispose();
        }
    }

    private static int RecordingReferenceCount(IGraphicsResource resource)
    {
        return ((IResourceGenerationOwner)resource).GetResourceRecord(0).RecordingReferenceCount;
    }

    private static int PendingSubmissionReferenceCount(IGraphicsResource resource)
    {
        return ((IResourceGenerationOwner)resource).GetResourceRecord(0).PendingSubmissionReferenceCount;
    }

    /// <summary>
    /// A pin that names a generation the record no longer carries is refused with its own identifier.
    /// </summary>
    /// <remarks>
    /// A generation is never replaced while it is pinned, so the condition is made at the guard's own input:
    /// the stored pin is replaced by one naming a generation the record never carried. The real pin is put
    /// back afterwards, so the recording reference it holds is still released.
    /// </remarks>
    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAPinWhoseGenerationTheRecordNoLongerCarries(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        PipelineHostRuntime host = PipelineSubmissionSetup.Host(device, out DeviceRegistrationRegistry registry, parameterCount: 1);

        using ReadWriteBuffer<int> buffer = graphicsDevice.AllocateReadWriteBuffer<int>(64);

        try
        {
            Assert.IsTrue(host.RecordingBundles.TryRent(out int bundleIndex));

            PipelineSubmissionSetup.Pin(host, bundleIndex, buffer);

            Span<ResourceGenerationPin> pins = ResourceGenerationPinTracker.GetPins(
                host.RecordingBundles.Storage,
                in host.RecordingBundles.GetBundle(bundleIndex));

            ResourceGenerationPin pinned = pins[0];

            pins[0] = new ResourceGenerationPin(
                pinned.Handle,
                new ResourceGenerationId(pinned.GenerationId.Value + 1),
                pinned.ResourceIndex);

            ComputeDiagnosticException rejection = Assert.ThrowsExactly<ComputeDiagnosticException>(
                () => ResourceGenerationPinTracker.Rollback(
                    graphicsDevice,
                    host.RecordingBundles.Storage,
                    ref host.RecordingBundles.GetBundle(bundleIndex)));

            Assert.AreEqual("CMPW1003", rejection.DiagnosticId);

            pins[0] = pinned;

            ResourceGenerationPinTracker.Rollback(
                graphicsDevice,
                host.RecordingBundles.Storage,
                ref host.RecordingBundles.GetBundle(bundleIndex));

            Assert.AreEqual(0, RecordingReferenceCount(buffer));

            host.RecordingBundles.Return(bundleIndex);
        }
        finally
        {
            registry.Dispose();
        }
    }
}
