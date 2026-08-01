using System;
using System.Linq;
using ComputeWeave.Memory;
using ComputeWeave.Tests.Attributes;
using ComputeWeave.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable IDE0051, IDE0060

namespace ComputeWeave.Tests.Internals;

[ComputeResourceGroup]
internal sealed partial class GeneratedGridResources
{
    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> Cells { get; } = null!;

    [ComputePipelineResource(ComputeResourceAccess.Read)]
    internal ReadOnlyTexture2D<float> Weights { get; } = null!;
}

[ComputePipelineHost("device", 1)]
internal sealed partial class GeneratedPipelineHost
{
    private readonly GraphicsDevice device;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceSlot<ReadWriteBuffer<int>> values = new();

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Discardable)]
    private readonly ComputeResourceSlot<ReadWriteTexture2D<float>> mask = new();

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceGroupSlot<GeneratedGridResources> grid = new();

    [ComputePipeline]
    private void Run(in ComputeContext context)
    {
        _ = this.device;

        context.Clear(GetValuesComputeBinding().Resource!);
        context.Clear(GetMaskComputeBinding().Resource!);
    }
}

[TestClass]
public class GeneratedPipelineHostTests
{
    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesAndBindsTheResourcesOfEveryOwnedSlot(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        try
        {
            Assert.IsFalse(host.GetValuesComputeBinding().IsValid);
            Assert.IsFalse(host.GetMaskComputeBinding().IsValid);

            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(1024), out bool changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> values = host.GetValuesComputeBinding();

            Assert.IsTrue(values.IsValid);
            Assert.AreEqual(1024, values.Resource!.Length);
            Assert.AreSame(graphicsDevice, values.Resource.GraphicsDevice);

            Assert.IsTrue(host.TryEnsureMask(new GeneratedPipelineHost.MaskPlan(64, 32), out changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteTexture2D<float>> mask = host.GetMaskComputeBinding();

            Assert.IsTrue(mask.IsValid);
            Assert.AreEqual(64, mask.Resource!.Width);
            Assert.AreEqual(32, mask.Resource.Height);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsTheActiveGenerationForAnIdenticalPlan(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        try
        {
            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(512), out bool changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> first = host.GetValuesComputeBinding();

            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(512), out changed));
            Assert.IsFalse(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> second = host.GetValuesComputeBinding();

            Assert.AreSame(first.Resource, second.Resource);
            Assert.AreEqual(first.BindingEpoch, second.BindingEpoch);

            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(256), out changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> third = host.GetValuesComputeBinding();

            Assert.AreNotSame(first.Resource, third.Resource);
            Assert.AreEqual(256, third.Resource!.Length);
            Assert.AreNotEqual(first.BindingEpoch, third.BindingEpoch);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesEveryMemberOfAnOwnedResourceGroup(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        try
        {
            Assert.IsTrue(host.TryEnsureGrid(new GeneratedGridResources.Plan(128, 16, 8), out bool changed));
            Assert.IsTrue(changed);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryOwnedResourceOnDispose(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        ulong before = GetOwnedBytes(graphicsDevice);

        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(4096), out _));
        Assert.IsTrue(host.TryEnsureGrid(new GeneratedGridResources.Plan(4096, 64, 64), out _));
        Assert.AreNotEqual(before, GetOwnedBytes(graphicsDevice));

        host.Dispose();
        host.WaitForDisposal();

        Assert.IsFalse(host.GetValuesComputeBinding().IsValid);
        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsAndSubmitsThroughTheGeneratedInvocationWrapper(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        try
        {
            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(64), out _));
            Assert.IsTrue(host.TryEnsureMask(new GeneratedPipelineHost.MaskPlan(8, 8), out _));
            Assert.IsTrue(host.TryEnsureGrid(new GeneratedGridResources.Plan(64, 8, 8), out _));

            ReadWriteBuffer<int> values = host.GetValuesComputeBinding().Resource!;
            ReadWriteTexture2D<float> mask = host.GetMaskComputeBinding().Resource!;

            values.CopyFrom(Enumerable.Range(1, 64).ToArray());

            using (ComputeContext context = graphicsDevice.CreateComputeContext())
            {
                context.Transition(mask, ResourceState.ReadOnly);
            }

            Assert.IsNotNull(mask.AsReadOnly());

            ComputeSubmission submission = host.Run();

            submission.Wait();

            Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);

            foreach (int value in values.ToArray())
            {
                Assert.AreEqual(0, value);
            }

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => mask.AsReadOnly());
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ObservesTheRegistrationAggregateOnTheAdmissionSnapshot(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        DeviceStructuralAggregate before = graphicsDevice.RefreshMemoryObservations().Structural;

        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 2);

        try
        {
            DeviceStructuralAggregate registered = graphicsDevice.RefreshMemoryObservations().Structural;

            Assert.AreEqual(before.RecordingBundleCount + 1, registered.RecordingBundleCount);
            Assert.AreEqual(before.PendingRecordCount + 2, registered.PendingRecordCount);
            Assert.AreEqual(before.UsageSetCount + 2, registered.UsageSetCount);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }

        DeviceStructuralAggregate released = graphicsDevice.RefreshMemoryObservations().Structural;

        Assert.AreEqual(before.RecordingBundleCount, released.RecordingBundleCount);
        Assert.AreEqual(before.PendingRecordCount, released.PendingRecordCount);
        Assert.AreEqual(before.UsageSetCount, released.UsageSetCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TrimsTheIdleGenerationsOfEveryOwnedSlot(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ulong before = GetOwnedBytes(graphicsDevice);
        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        try
        {
            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(64), out _));
            Assert.IsTrue(host.TryEnsureMask(new GeneratedPipelineHost.MaskPlan(8, 8), out _));
            Assert.IsTrue(host.TryEnsureGrid(new GeneratedGridResources.Plan(64, 8, 8), out _));

            GraphicsMemoryStatistics allocated = graphicsDevice.GetMemoryStatistics();

            Assert.AreEqual(4, allocated.ActiveGenerationCount);
            Assert.AreEqual(0, allocated.RetiredGenerationCount);
            Assert.IsTrue(GetOwnedBytes(graphicsDevice) > before);

            host.Run().Wait();

            graphicsDevice.TrimMemory();

            GraphicsMemoryStatistics trimmed = graphicsDevice.GetMemoryStatistics();

            Assert.AreEqual(0, trimmed.ActiveGenerationCount);
            Assert.AreEqual(0, trimmed.RetiredGenerationCount);
            Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

            Assert.IsFalse(host.GetValuesComputeBinding().IsValid);
            Assert.IsTrue(host.TryEnsureValues(new GeneratedPipelineHost.ValuesPlan(64), out _));
            Assert.IsTrue(host.GetValuesComputeBinding().IsValid);
        }
        finally
        {
            host.Dispose();
            host.WaitForDisposal();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void TrimsTheManagedPoolSurplusLeftByAnUnregisteredHost(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        graphicsDevice.TrimMemory();

        Assert.AreEqual(0, graphicsDevice.GetMemoryStatistics().ManagedPoolSurplusCount);

        GeneratedPipelineHost host = GeneratedPipelineHost.Create(graphicsDevice, 1);

        host.Dispose();
        host.WaitForDisposal();

        Assert.AreEqual(1, graphicsDevice.GetMemoryStatistics().ManagedPoolSurplusCount);

        graphicsDevice.TrimMemory();

        Assert.AreEqual(0, graphicsDevice.GetMemoryStatistics().ManagedPoolSurplusCount);
    }

    private static ulong GetOwnedBytes(GraphicsDevice device)
    {
        GraphicsMemoryStatistics statistics = device.GetMemoryStatistics();

        return statistics.Local.ComputeWeaveOwnedBytes + statistics.NonLocal.ComputeWeaveOwnedBytes;
    }
}
