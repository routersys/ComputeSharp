using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using ComputeSharp.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe class InteropRoundTripTests
{
    private const string CanonicalSignature = "H|M|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext";

    private sealed class Fixture(GraphicsDevice device) : IDisposable
    {
        public GraphicsDevice Device { get; } = device;

        public FakeInteropScheduler Scheduler { get; } = new();

        public FakeInteropProvider Provider { get; private set; } = null!;

        public ComputeInteropDomain Domain { get; private set; } = null!;

        public ComputeInteropResourceSetRuntime Resources { get; private set; } = null!;

        public SharedTextureSlot<Bgra32, Float4, FakeExternalView> Slot { get; } = new();

        public DeviceRegistrationRegistry Registry { get; private set; } = null!;

        public PipelineHostRuntime Host { get; private set; } = null!;

        public CompletionRegistry Completions { get; } = new();

        public Fixture Register(ComputeSharedTextureInitialOwner initialOwner)
        {
            Provider = new FakeInteropProvider(Device, Scheduler);
            Domain = Device.RegisterExternalDomain(Provider);
            Resources = ComputeInteropResourceSetRuntime.Create(Device, Domain, ResourceSetDescriptor(initialOwner), [Slot]);
            Registry = new DeviceRegistrationRegistry(Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);
            Host = Registry.RegisterHost(InteropHostDescriptor(), maximumPendingSubmissions: 2, []);

            Assert.IsTrue(Slot.TryEnsure(16, 16, out _));

            return this;
        }

        public ReadWriteTexture2D<Bgra32, Float4> Texture => Slot.GetComputeBinding().Resource!;

        public ref ResourceGenerationRecord Record => ref GetOwner().GetResourceRecord(0);

        public ResourceGenerationOwner GetOwner()
        {
            Assert.IsTrue(((IGenerationBoundResource)Texture).TryGetGenerationBinding(out ResourceUsageBinding binding));

            return (ResourceGenerationOwner)binding.Set.Owner;
        }

        public void Dispose()
        {
            Registry.Dispose();
            Resources.Dispose();
            Resources.WaitForDisposal();
            Domain.Dispose();
            Scheduler.Dispose();
        }
    }

    private static void WriteInt32(List<byte> payload, int value)
    {
        byte[] buffer = new byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);

        payload.AddRange(buffer);
    }

    private static void WriteString(List<byte> payload, string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);

        WriteInt32(payload, utf8.Length);

        payload.AddRange(utf8);
    }

    private static byte[] Assemble(byte[] payload)
    {
        ReadOnlySpan<byte> header = [0x43, 0x53, 0x50, 0x31, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00];
        byte[] hashInput = new byte[header.Length + payload.Length];

        header.CopyTo(hashInput);
        payload.CopyTo(hashInput, header.Length);

        byte[] descriptor = new byte[48 + payload.Length];

        header.CopyTo(descriptor);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12, 4), (uint)payload.Length);
        SHA256.HashData(hashInput).CopyTo(descriptor, 16);
        payload.CopyTo(descriptor, 48);

        return descriptor;
    }

    private static byte[] ResourceSetDescriptor(ComputeSharedTextureInitialOwner initialOwner)
    {
        List<byte> payload = [(byte)DescriptorKind.InteropResourceSet];

        WriteString(payload, "R");
        WriteInt32(payload, 1);
        WriteInt32(payload, 1);
        WriteInt32(payload, 0);
        WriteString(payload, "M0");
        WriteString(payload, "T");

        payload.Add((byte)ComputeResourceResizePolicy.Exact);
        payload.Add((byte)ComputeResourceAccess.ReadWrite);
        payload.Add((byte)ExternalResourceAccess.Write);
        payload.Add((byte)ExternalTextureUsage.RenderTarget);
        payload.Add((byte)ComputeAlphaMode.Premultiplied);
        payload.Add((byte)initialOwner);
        payload.Add((byte)ComputeResourceRecovery.RecreateFromHost);

        return Assemble([.. payload]);
    }

    private static byte[] InteropHostDescriptor()
    {
        List<byte> payload = [(byte)DescriptorKind.PipelineHost];

        WriteString(payload, "H");
        WriteInt32(payload, 1);
        WriteInt32(payload, 1);
        WriteInt32(payload, 3);
        WriteInt32(payload, 0);
        WriteInt32(payload, 1);

        WriteInt32(payload, 0);
        WriteString(payload, "M");
        WriteString(payload, CanonicalSignature);
        WriteInt32(payload, (int)PipelineFlags.InteropRoundTrip);
        WriteInt32(payload, 1);
        WriteInt32(payload, 3);
        WriteInt32(payload, 1);

        WriteInt32(payload, 0);
        WriteString(payload, "ComputeSharp.ReadWriteTexture2D`2[ComputeSharp.Bgra32,ComputeSharp.Float4]");

        payload.Add((byte)ComputeResourceAccess.ReadWrite);
        payload.Add((byte)ComputeResourceSharing.External);
        payload.Add((byte)ComputeResourceAliasing.Disallow);
        payload.Add((byte)ResourceOwnershipKind.Borrowed);
        payload.Add(0);

        WriteInt32(payload, 0);
        WriteInt32(payload, 0);

        WriteInt32(payload, 0);
        WriteInt32(payload, 0);

        return Assemble([.. payload]);
    }

    private static Fixture Create(Device device, ComputeSharedTextureInitialOwner initialOwner)
    {
        return new Fixture(device.Get()).Register(initialOwner);
    }

    private static ComputeSubmission Submit(Fixture fixture, ulong submissionSequence)
    {
        PipelineKey pipeline = new(fixture.Host.Id, new PipelineOrdinal(0));

        Assert.IsTrue(fixture.Host.TryCheckoutPendingRecord(pipeline, submissionSequence, out int index));

        ref PendingSubmissionRecord record = ref fixture.Host.PendingRecords.GetRecord(index);

        Assert.IsTrue(record.TryBeginRecording());

        fixture.Host.CommandLists.Rent(null, out ID3D12GraphicsCommandList* d3D12CommandList, out ID3D12CommandAllocator* d3D12CommandAllocator);

        _ = d3D12CommandList->Close();

        SubmissionRetention retention = new() { ResourceUsages = fixture.Host.GetUsageSetHandle(index) };

        Assert.IsTrue(retention.CommandLists.TryAdd((nint)d3D12CommandList, (nint)d3D12CommandAllocator, ComputeQueueKind.Compute));

        ReadWriteTexture2D<Bgra32, Float4> texture = fixture.Texture;

        Assert.IsTrue(ResourceGenerationPinTracker.TryPin(
            fixture.Host.Device,
            fixture.Host.RecordingBundles.Storage,
            ref fixture.Host.RecordingBundles.GetBundle(0),
            texture));

        fixture.Host.CreateUsageRecorder(index).RecordWrite(texture);

        return ComputeSubmissionExecutor.SubmitInterop(
            fixture.Device,
            fixture.Host,
            fixture.Completions,
            index,
            bundleIndex: 0,
            ref retention);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RunsTheNormalRoundTripOfAnExternallyOwnedSharedTexture(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Record.ReadOwnership());

        ComputeSubmission submission = Submit(fixture, 1);

        Assert.AreEqual(1, fixture.Provider.SignalCount);
        Assert.AreEqual(1, fixture.Provider.FlushCount);
        Assert.AreEqual(1, fixture.Provider.WaitCount);
        Assert.IsTrue(fixture.Provider.ObservedSignalValue < fixture.Provider.ObservedWaitValue);
        Assert.IsTrue(fixture.Provider.WasReservedWhileSignaling);
        Assert.IsTrue(fixture.Provider.WasReservedWhileWaiting);
        Assert.IsFalse(fixture.Scheduler.IsReserved);

        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Record.ReadOwnership());
        Assert.AreEqual(TrackedResourceState.Common, fixture.Record.D3D12State);
        Assert.AreEqual(submission.Completion.Value, fixture.Record.LastWrite.Value);

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(fixture.Device, fixture.Completions));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesAComputeOwnedSharedTextureWithoutAcquiringIt(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.Compute);

        Assert.AreEqual(ExternalOwnershipState.ComputeAvailable, fixture.Record.ReadOwnership());

        ComputeSubmission submission = Submit(fixture, 1);

        Assert.AreEqual(0, fixture.Provider.SignalCount);
        Assert.AreEqual(0, fixture.Provider.FlushCount);
        Assert.AreEqual(1, fixture.Provider.WaitCount);

        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Record.ReadOwnership());
        Assert.AreEqual(TrackedResourceState.Common, fixture.Record.D3D12State);

        submission.Wait();

        Assert.AreEqual(ComputeSubmissionStatus.Succeeded, submission.Status);
        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(fixture.Device, fixture.Completions));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AcquiresTheSharedTextureOnEverySuccessiveRoundTrip(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.Compute);

        ComputeSubmission first = Submit(fixture, 1);

        first.Wait();

        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(fixture.Device, fixture.Completions));

        ComputeSubmission second = Submit(fixture, 2);

        Assert.AreEqual(1, fixture.Provider.SignalCount);
        Assert.AreEqual(2, fixture.Provider.WaitCount);
        Assert.AreEqual(ExternalOwnershipState.ExternalAvailable, fixture.Record.ReadOwnership());

        second.Wait();

        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(fixture.Device, fixture.Completions));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RecordsTheEpilogueThatHandsTheSharedTextureBack(Device device)
    {
        using Fixture fixture = Create(device, ComputeSharedTextureInitialOwner.External);

        ComputeSubmission submission = Submit(fixture, 1);

        Assert.AreEqual(3, fixture.Host.CommandLists.AvailableCount);

        submission.Wait();

        Assert.IsTrue(ComputeSubmissionExecutor.TryReleaseCompleted(fixture.Device, fixture.Completions));

        Assert.AreEqual(6, fixture.Host.CommandLists.AvailableCount);
    }
}
