using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Lifetime;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ComputeSharp.Win32.D3D12_COMMAND_LIST_TYPE;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class DeviceRegistrationRegistryTests
{
    private const string CanonicalSignature = "H|M|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext";

    private static void WriteString(List<byte> payload, string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);

        WriteInt32(payload, utf8.Length);

        payload.AddRange(utf8);
    }

    private static void WriteInt32(List<byte> payload, int value)
    {
        byte[] buffer = new byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);

        payload.AddRange(buffer);
    }

    private static byte[] HostPayload(int slotCount)
    {
        List<byte> payload = [(byte)DescriptorKind.PipelineHost];

        WriteString(payload, "H");
        WriteInt32(payload, 1);
        WriteInt32(payload, 0);
        WriteInt32(payload, 1);
        WriteInt32(payload, slotCount);
        WriteInt32(payload, 1);

        WriteInt32(payload, 0);
        WriteString(payload, "M");
        WriteString(payload, CanonicalSignature);
        WriteInt32(payload, 0);
        WriteInt32(payload, 0);
        WriteInt32(payload, 1);
        WriteInt32(payload, 0);
        WriteInt32(payload, 0);

        WriteInt32(payload, slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            WriteInt32(payload, i);
            WriteString(payload, $"S{i}");
            WriteString(payload, "ComputeSharp.ReadWriteBuffer`1[System.Int32]");
            payload.Add((byte)ResourceOwnershipKind.OwnedSlot);
            payload.Add((byte)ResourcePlanKind.Buffer);
            payload.Add((byte)ComputeResourceRecovery.Discardable);

            WriteInt32(payload, 1);
            WriteInt32(payload, 0);
            WriteInt32(payload, 0);
            WriteString(payload, $"S{i}");
            WriteString(payload, "ComputeSharp.ReadWriteBuffer`1[System.Int32]");
            WriteString(payload, $"s{i}Length");
            payload.Add((byte)ResourcePlanDimensionKind.Length);
        }

        return [.. payload];
    }

    internal static byte[] CreateHostDescriptor(int slotCount)
    {
        byte[] payload = HostPayload(slotCount);
        ReadOnlySpan<byte> header = [0x43, 0x53, 0x50, 0x31, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00];
        byte[] hashInput = new byte[10 + payload.Length];

        header.CopyTo(hashInput);
        payload.CopyTo(hashInput, 10);

        byte[] hash = SHA256.HashData(hashInput);
        byte[] descriptor = new byte[48 + payload.Length];

        header.CopyTo(descriptor);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(12, 4), (uint)payload.Length);
        hash.CopyTo(descriptor, 16);
        payload.CopyTo(descriptor, 48);

        return descriptor;
    }

    private static IComputeOwnedSlot[] Slots(int count)
    {
        IComputeOwnedSlot[] slots = new IComputeOwnedSlot[count];

        for (int i = 0; i < count; i++)
        {
            slots[i] = new ComputeResourceSlot<ReadWriteBuffer<int>>();
        }

        return slots;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesHostWithEveryStructuralReservation(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            IComputeOwnedSlot[] slots = Slots(2);
            PipelineHostRuntime runtime = registry.RegisterHost(CreateHostDescriptor(2), maximumPendingSubmissions: 3, slots);

            Assert.AreEqual(1ul, runtime.Id.Value);
            Assert.AreEqual(RegistrationState.Active, runtime.State);
            Assert.AreEqual(1, registry.HostCount);

            Assert.AreEqual(3, runtime.PendingRecords.Capacity);
            Assert.AreEqual(3, runtime.CommandLists.Capacity);
            Assert.AreEqual(3, runtime.UsageSets.SetCount);
            Assert.AreEqual(6, runtime.PlanStorage.Length);
            Assert.AreEqual(2, runtime.PlanStates.Length);

            DeviceStructuralAggregate aggregate = registry.Aggregate;

            Assert.AreEqual(1, aggregate.RecordingBundleCount);
            Assert.AreEqual(3, aggregate.PendingRecordCount);
            Assert.AreEqual(3, aggregate.UsageSetCount);
            Assert.AreEqual(3, aggregate.CommandListEntryCount);
            Assert.AreEqual(0, aggregate.UsageEntryStorageCount);
            Assert.AreEqual(2, aggregate.PreparedReplacementRecordCount);
            Assert.AreEqual(2, aggregate.DeferredGenerationRecordCount);
            Assert.AreEqual(6, aggregate.HostPlanScalarCount);

            foreach (IComputeOwnedSlot slot in slots)
            {
                Assert.IsFalse(slot.IsDisposalComplete);
            }
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AssignsIncreasingRegistrationIdentities(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            PipelineHostRuntime first = registry.RegisterHost(CreateHostDescriptor(1), 1, Slots(1));
            PipelineHostRuntime second = registry.RegisterHost(CreateHostDescriptor(1), 1, Slots(1));

            Assert.AreEqual(1ul, first.Id.Value);
            Assert.AreEqual(2ul, second.Id.Value);
            Assert.AreEqual(2, registry.HostCount);
            Assert.AreNotEqual(first.UsageSets.GetHandle(0), second.UsageSets.GetHandle(0));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RollsBackEveryReservationWhenSlotBindingFails(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            IComputeOwnedSlot[] bound = Slots(1);

            _ = registry.RegisterHost(CreateHostDescriptor(1), 1, bound);

            DeviceStructuralAggregate afterFirst = registry.Aggregate;

            IComputeOwnedSlot[] reused = [Slots(1)[0], bound[0]];

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => registry.RegisterHost(CreateHostDescriptor(2), 2, reused));

            DeviceStructuralAggregate afterFailure = registry.Aggregate;

            Assert.AreEqual(1, registry.HostCount);
            Assert.AreEqual(afterFirst.PendingRecordCount, afterFailure.PendingRecordCount);
            Assert.AreEqual(afterFirst.CommandListEntryCount, afterFailure.CommandListEntryCount);
            Assert.AreEqual(afterFirst.UsageSetCount, afterFailure.UsageSetCount);
            Assert.AreEqual(afterFirst.HostPlanScalarCount, afterFailure.HostPlanScalarCount);
            Assert.AreEqual(afterFirst.RecordingBundleCount, afterFailure.RecordingBundleCount);

            Assert.IsTrue(reused[0].IsDisposalComplete);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsDescriptorAndArgumentMismatch(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            _ = Assert.ThrowsExactly<ArgumentNullException>(() => registry.RegisterHost(CreateHostDescriptor(1), 1, null!));
            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => registry.RegisterHost(CreateHostDescriptor(1), 0, Slots(1)));
            _ = Assert.ThrowsExactly<ArgumentException>(() => registry.RegisterHost(CreateHostDescriptor(1), 1, Slots(2)));

            Assert.AreEqual(0, registry.HostCount);
            Assert.AreEqual(0, registry.Aggregate.PendingRecordCount);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesAggregateOnlyAfterSlotDisposalCompletes(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            IComputeOwnedSlot[] slots = Slots(1);
            PipelineHostRuntime runtime = registry.RegisterHost(CreateHostDescriptor(1), 2, slots);

            Assert.IsFalse(registry.TryUnregisterHost(runtime));
            Assert.AreEqual(1, registry.HostCount);

            runtime.RequestDispose();

            Assert.AreEqual(RegistrationState.DisposeRequested, runtime.State);
            Assert.IsTrue(slots[0].IsDisposalComplete);

            Assert.IsTrue(registry.TryUnregisterHost(runtime));
            Assert.AreEqual(RegistrationState.Released, runtime.State);
            Assert.AreEqual(0, registry.HostCount);

            DeviceStructuralAggregate aggregate = registry.Aggregate;

            Assert.AreEqual(0, aggregate.PendingRecordCount);
            Assert.AreEqual(0, aggregate.CommandListEntryCount);
            Assert.AreEqual(0, aggregate.HostPlanScalarCount);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsPendingReservationsWithinTheHostContract(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        try
        {
            PipelineHostRuntime runtime = registry.RegisterHost(CreateHostDescriptor(1), 2, Slots(1));
            PipelineKey pipeline = new(runtime.Id, new PipelineOrdinal(0));

            Assert.IsTrue(runtime.TryAcquireInvocation());
            Assert.IsFalse(runtime.TryAcquireInvocation());

            Assert.IsTrue(runtime.TryCheckoutPendingRecord(pipeline, 1, out int first));
            Assert.IsTrue(runtime.TryCheckoutPendingRecord(pipeline, 2, out int second));
            Assert.IsFalse(runtime.TryCheckoutPendingRecord(pipeline, 3, out _));

            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(runtime.GetUsageSetHandle(first), runtime.GetUsageSetHandle(second));

            Assert.IsTrue(runtime.PendingRecords.GetRecord(first).TryAbort());

            runtime.ReturnPendingRecord(first);

            Assert.IsTrue(runtime.TryCheckoutPendingRecord(pipeline, 4, out int reused));
            Assert.AreEqual(first, reused);

            runtime.ReleaseInvocation();

            Assert.IsTrue(runtime.TryAcquireInvocation());
        }
        finally
        {
            registry.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsRegistrationAfterDeviceDisposal(Device device)
    {
        DeviceRegistrationRegistry registry = new(device.Get().D3D12Device, D3D12_COMMAND_LIST_TYPE_COMPUTE);

        PipelineHostRuntime runtime = registry.RegisterHost(CreateHostDescriptor(1), 1, Slots(1));

        registry.Dispose();
        registry.Dispose();

        Assert.AreEqual(RegistrationState.DisposeRequested, runtime.State);
        Assert.AreEqual(0, registry.HostCount);

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => registry.RegisterHost(CreateHostDescriptor(1), 1, Slots(1)));
    }
}
