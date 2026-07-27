using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ComputeSharp.Memory;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class InteropResourceSetRegistrationTests
{
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

    private static byte[] ResourceSetDescriptor(int sharedTextureSlotCount)
    {
        List<byte> payload = [1];

        WriteString(payload, "R");
        WriteInt32(payload, sharedTextureSlotCount);
        WriteInt32(payload, sharedTextureSlotCount);

        for (int i = 0; i < sharedTextureSlotCount; i++)
        {
            WriteInt32(payload, i);
            WriteString(payload, $"M{i}");
            WriteString(payload, "T");

            payload.Add((byte)ComputeResourceResizePolicy.Exact);
            payload.Add((byte)ComputeResourceAccess.ReadWrite);
            payload.Add((byte)ExternalResourceAccess.Write);
            payload.Add((byte)ExternalTextureUsage.RenderTarget);
            payload.Add((byte)ComputeAlphaMode.Premultiplied);
            payload.Add((byte)ComputeSharedTextureInitialOwner.External);
            payload.Add((byte)ComputeResourceRecovery.RecreateFromHost);
        }

        return Assemble([.. payload]);
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

    private static byte[] HostDescriptor()
    {
        return Assemble(
        [
            0x00,
            0x01, 0x00, 0x00, 0x00, 0x48,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x4D,
            0x40, 0x00, 0x00, 0x00,
            0x48, 0x7C, 0x4D, 0x7C, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x7C, 0x53, 0x79, 0x73,
            0x74, 0x65, 0x6D, 0x2E, 0x56, 0x6F, 0x69, 0x64, 0x7C, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
            0x31, 0x7C, 0x30, 0x33, 0x3A, 0x43, 0x6F, 0x6D, 0x70, 0x75, 0x74, 0x65, 0x53, 0x68, 0x61, 0x72,
            0x70, 0x2E, 0x43, 0x6F, 0x6D, 0x70, 0x75, 0x74, 0x65, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x78, 0x74,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ]);
    }

    private static IComputeSharedResourceSlot[] CreateSlots(int count)
    {
        IComputeSharedResourceSlot[] slots = new IComputeSharedResourceSlot[count];

        for (int i = 0; i < count; i++)
        {
            slots[i] = new SharedTextureSlot<Bgra32, Float4, FakeExternalView>();
        }

        return slots;
    }

    private static ComputeInteropDomain RegisterDomain(GraphicsDevice device, FakeInteropScheduler scheduler)
    {
        return device.RegisterExternalDomain(new FakeInteropProvider(device, scheduler));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RegistersAResourceSetAgainstItsDomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        IComputeSharedResourceSlot[] slots = CreateSlots(2);

        using ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(2),
            slots);

        Assert.AreSame(graphicsDevice, resources.Device);
        Assert.AreSame(domain, resources.Domain);
        Assert.IsFalse(resources.IsDisposeRequested);

        foreach (IComputeSharedResourceSlot slot in slots)
        {
            SharedTextureSlot<Bgra32, Float4, FakeExternalView> sharedSlot = (SharedTextureSlot<Bgra32, Float4, FakeExternalView>)slot;

            Assert.IsFalse(sharedSlot.IsAllocated);
            Assert.AreEqual(0, sharedSlot.Width);
            Assert.AreEqual(0, sharedSlot.Height);
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReservesAndReturnsTheStructuralBaselineOfAResourceSet(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        DeviceStructuralAggregate before = graphicsDevice.GetRegistrationAggregate();

        ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(3),
            CreateSlots(3));

        DeviceStructuralAggregate reserved = graphicsDevice.GetRegistrationAggregate();

        Assert.AreEqual(before.SharedTextureSlotCount + 3, reserved.SharedTextureSlotCount);
        Assert.AreEqual(before.MaintenanceRecordCount + 3, reserved.MaintenanceRecordCount);
        Assert.AreEqual(before.PersistentLeaseCapacity + 6, reserved.PersistentLeaseCapacity);
        Assert.AreEqual(before.ResourceSetPlanScalarCount + 18, reserved.ResourceSetPlanScalarCount);

        resources.Dispose();

        DeviceStructuralAggregate released = graphicsDevice.GetRegistrationAggregate();

        Assert.AreEqual(before.SharedTextureSlotCount, released.SharedTextureSlotCount);
        Assert.AreEqual(before.MaintenanceRecordCount, released.MaintenanceRecordCount);
        Assert.AreEqual(before.PersistentLeaseCapacity, released.PersistentLeaseCapacity);
        Assert.AreEqual(before.ResourceSetPlanScalarCount, released.ResourceSetPlanScalarCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AResourceSetHoldsItsDomainUntilItIsDisposed(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            CreateSlots(1));

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposeRequested);
        Assert.IsFalse(domain.IsDisposed);
        Assert.AreEqual(0, provider.DisposeCount);

        resources.Dispose();

        Assert.IsTrue(resources.IsDisposeRequested);
        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);

        resources.WaitForDisposal();
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsASlotCountThatDoesNotMatchTheDescriptor(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        _ = Assert.ThrowsException<ArgumentException>(() => ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(2),
            CreateSlots(1)));

        Assert.IsFalse(domain.IsDisposed);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsAHostDescriptorAsAResourceSet(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        _ = Assert.ThrowsException<ArgumentException>(() => ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            HostDescriptor(),
            []));

        Assert.IsFalse(domain.IsDisposed);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsARegistrationAgainstADisposedDomain(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        domain.Dispose();

        Assert.IsTrue(domain.IsDisposed);

        _ = Assert.ThrowsException<InvalidOperationException>(() => ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            CreateSlots(1)));

        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsASharedSlotThatIsAlreadyBound(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        IComputeSharedResourceSlot[] slots = CreateSlots(1);
        SharedTextureSlot<Bgra32, Float4, FakeExternalView> slot = (SharedTextureSlot<Bgra32, Float4, FakeExternalView>)slots[0];

        ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            slots);

        _ = Assert.ThrowsException<InvalidOperationException>(() => ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            slots));

        Assert.IsFalse(slot.IsDisposeRequested);
        Assert.IsFalse(resources.IsDisposeRequested);

        resources.Dispose();

        Assert.IsTrue(slot.IsDisposeRequested);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AFailedRegistrationReleasesTheDomainReferenceItTook(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();

        FakeInteropProvider provider = new(graphicsDevice, scheduler);
        ComputeInteropDomain domain = graphicsDevice.RegisterExternalDomain(provider);

        IComputeSharedResourceSlot[] slots = CreateSlots(1);

        ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            slots);

        _ = Assert.ThrowsException<InvalidOperationException>(() => ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            slots));

        resources.Dispose();
        domain.Dispose();

        Assert.IsTrue(domain.IsDisposed);
        Assert.AreEqual(1, provider.DisposeCount);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void WaitingForTheDisposalOfALiveResourceSetIsRejected(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        using FakeInteropScheduler scheduler = new();
        using ComputeInteropDomain domain = RegisterDomain(graphicsDevice, scheduler);

        using ComputeInteropResourceSetRuntime resources = ComputeInteropResourceSetRuntime.Create(
            graphicsDevice,
            domain,
            ResourceSetDescriptor(1),
            CreateSlots(1));

        _ = Assert.ThrowsException<InvalidOperationException>(resources.WaitForDisposal);
    }

    [TestMethod]
    public void AnUnboundSharedSlotRejectsItsOperationsAndWaitsForNothing()
    {
        using SharedTextureSlot<Bgra32, Float4, FakeExternalView> slot = new();

        Assert.IsFalse(slot.IsAllocated);
        Assert.AreEqual(0, slot.Width);
        Assert.AreEqual(0, slot.Height);

        _ = Assert.ThrowsException<InvalidOperationException>(() => _ = slot.TryEnsure(4, 4, out _));
        _ = Assert.ThrowsException<InvalidOperationException>(() => _ = slot.GetComputeBinding());
        _ = Assert.ThrowsException<InvalidOperationException>(() => { _ = slot.BeginExternalOperation().IsValid; });
        _ = Assert.ThrowsException<InvalidOperationException>(() => _ = slot.AcquireExternalViewLease());

        slot.WaitForDisposal();
    }
}
