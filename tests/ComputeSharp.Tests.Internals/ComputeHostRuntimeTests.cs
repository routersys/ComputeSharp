using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ComputeHostRuntimeTests
{
    private const string CanonicalSignature = "H|M|00000000|System.Void|00000001|03:ComputeSharp.ComputeContext";

    private const string BufferTypeMetadataName = "ComputeSharp.ReadWriteBuffer`1[System.Int32]";

    private readonly struct BufferMaterializer(int length) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareBuffer<int>(length);
        }
    }

    private readonly struct GroupMaterializer(int firstLength, int secondLength) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareBuffer<int>(firstLength);
            context.DeclareBuffer<int>(secondLength);
        }
    }

    private readonly struct PartialGroupMaterializer(int firstLength) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareBuffer<int>(firstLength);
        }
    }

    private readonly struct Texture2DMaterializer(int width, int height) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareTexture2D<float>(width, height);
        }
    }

    private readonly struct DriftingMaterializer(int[] lengths) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareBuffer<int>(lengths[0]);

            lengths[0] = lengths[1];
        }
    }

    private readonly struct ThrowingMaterializer(int length) : IComputeGenerationMaterializer
    {
        public void Materialize(ref ComputeGenerationContext context)
        {
            context.DeclareBuffer<int>(length);

            throw new NotSupportedException();
        }
    }

    private static void WriteUInt32(List<byte> payload, uint value)
    {
        byte[] buffer = new byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);

        payload.AddRange(buffer);
    }

    private static void WriteString(List<byte> payload, string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);

        WriteUInt32(payload, (uint)utf8.Length);

        payload.AddRange(utf8);
    }

    private static void WriteInternalResource(List<byte> payload, uint ordinal, ComputeResourceAccess access, ResourceOwnershipKind ownership, uint slotResourceIndex)
    {
        WriteUInt32(payload, ordinal);
        WriteString(payload, BufferTypeMetadataName);
        payload.Add((byte)access);
        payload.Add((byte)ComputeResourceSharing.Internal);
        payload.Add((byte)ComputeResourceAliasing.Disallow);
        payload.Add((byte)ownership);
        payload.Add(1);
        WriteUInt32(payload, 0);
        WriteUInt32(payload, slotResourceIndex);
    }

    private static void WritePlanField(List<byte> payload, uint ordinal, uint slotResourceIndex, string planParameterName, ResourcePlanDimensionKind dimensionKind)
    {
        WriteUInt32(payload, ordinal);
        WriteUInt32(payload, slotResourceIndex);
        WriteString(payload, "S");
        WriteString(payload, BufferTypeMetadataName);
        WriteString(payload, planParameterName);
        payload.Add((byte)dimensionKind);
    }

    private static byte[] CreateDescriptor(
        ResourcePlanKind planKind,
        ComputeResourceAccess access = ComputeResourceAccess.ReadWrite,
        int resourceCount = 1,
        bool hasAccessContracts = true)
    {
        ResourceOwnershipKind ownership = planKind is ResourcePlanKind.ResourceGroup
            ? ResourceOwnershipKind.OwnedGroupSlot
            : ResourceOwnershipKind.OwnedSlot;

        int internalCount = hasAccessContracts ? resourceCount : 0;

        List<byte> payload = [(byte)DescriptorKind.PipelineHost];

        WriteString(payload, "H");
        WriteUInt32(payload, 1);
        WriteUInt32(payload, (uint)internalCount);
        WriteUInt32(payload, (uint)(internalCount > 0 ? 2 : 1));
        WriteUInt32(payload, 1);

        WriteUInt32(payload, 1);
        WriteUInt32(payload, 0);
        WriteString(payload, "M");
        WriteString(payload, CanonicalSignature);
        WriteUInt32(payload, 0);
        WriteUInt32(payload, (uint)internalCount);
        WriteUInt32(payload, (uint)(internalCount > 0 ? 2 : 1));
        WriteUInt32(payload, 0);
        WriteUInt32(payload, (uint)internalCount);

        for (int i = 0; i < internalCount; i++)
        {
            WriteInternalResource(payload, (uint)i, access, ownership, ownership is ResourceOwnershipKind.OwnedSlot ? 0u : (uint)i);
        }

        WriteUInt32(payload, 1);
        WriteUInt32(payload, 0);
        WriteString(payload, "S");
        WriteString(payload, BufferTypeMetadataName);
        payload.Add((byte)ownership);
        payload.Add((byte)planKind);
        payload.Add((byte)ComputeResourceRecovery.Discardable);

        if (planKind is ResourcePlanKind.Texture2D)
        {
            WriteUInt32(payload, 2);
            WritePlanField(payload, 0, 0, "sWidth", ResourcePlanDimensionKind.Width);
            WritePlanField(payload, 1, 0, "sHeight", ResourcePlanDimensionKind.Height);
        }
        else
        {
            WriteUInt32(payload, (uint)resourceCount);

            for (int i = 0; i < resourceCount; i++)
            {
                WritePlanField(payload, (uint)i, (uint)i, $"s{i}Length", ResourcePlanDimensionKind.Length);
            }
        }

        return Seal([.. payload]);
    }

    private static byte[] Seal(byte[] payload)
    {
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

    private static ulong GetExpectedBufferBytes(GraphicsDevice device, int length)
    {
        GraphicsCommittedResourceDescription description = ID3D12DeviceExtensions.GetCommittedResourceDescription(
            ResourceType.ReadWrite,
            (ulong)length * sizeof(int),
            device.IsCacheCoherentUMA);

        return device.D3D12Device->GetResourceAllocationInfo(in description).SizeInBytes;
    }

    private static ulong GetStableOwnedBytes(GraphicsDevice device)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        GraphicsMemoryStatistics statistics = device.GetMemoryStatistics();

        return statistics.Local.ComputeSharpOwnedBytes + statistics.NonLocal.ComputeSharpOwnedBytes;
    }

    private static ulong GetOwnedBytes(GraphicsDevice device)
    {
        GraphicsMemoryStatistics statistics = device.GetMemoryStatistics();

        return statistics.Local.ComputeSharpOwnedBytes + statistics.NonLocal.ComputeSharpOwnedBytes;
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesAGenerationMatchingTheRequestedPlan(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        try
        {
            Assert.IsFalse(slot.IsAllocated);
            Assert.IsFalse(host.GetBinding<ReadWriteBuffer<int>>(0, 0).IsValid);

            Assert.IsTrue(host.TryEnsureResource(0, [1024], new BufferMaterializer(1024), out bool changed));
            Assert.IsTrue(changed);
            Assert.IsTrue(slot.IsAllocated);

            ComputeResourceBinding<ReadWriteBuffer<int>> binding = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.IsTrue(binding.IsValid);
            Assert.AreEqual(1024, binding.Resource!.Length);
            Assert.AreSame(graphicsDevice, binding.Resource.GraphicsDevice);
            Assert.AreNotEqual(0ul, binding.SetId.Value);
            Assert.AreNotEqual(0ul, binding.GenerationId.Value);
            Assert.AreEqual(1ul, binding.BindingEpoch);
            Assert.AreEqual(0, binding.ResourceIndex);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void KeepsTheActiveGenerationForAnIdenticalPlan(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [512], new BufferMaterializer(512), out bool changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> first = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.IsTrue(host.TryEnsureResource(0, [512], new BufferMaterializer(512), out changed));
            Assert.IsFalse(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> second = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.AreSame(first.Resource, second.Resource);
            Assert.AreEqual(first.SetId, second.SetId);
            Assert.AreEqual(first.GenerationId, second.GenerationId);
            Assert.AreEqual(first.BindingEpoch, second.BindingEpoch);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReplacesTheActiveGenerationForADifferentPlan(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [256], new BufferMaterializer(256), out _));

            ComputeResourceBinding<ReadWriteBuffer<int>> first = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.IsTrue(host.TryEnsureResource(0, [768], new BufferMaterializer(768), out bool changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> second = host.GetBinding<ReadWriteBuffer<int>>(0, 0);

            Assert.AreNotSame(first.Resource, second.Resource);
            Assert.AreNotEqual(first.SetId, second.SetId);
            Assert.AreNotEqual(first.GenerationId, second.GenerationId);
            Assert.AreEqual(first.BindingEpoch + 1, second.BindingEpoch);
            Assert.AreEqual(768, second.Resource!.Length);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesEveryMemberOfAResourceGroup(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceGroupSlot<object> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            CreateDescriptor(ResourcePlanKind.ResourceGroup, resourceCount: 2),
            1,
            [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [128, 256], new GroupMaterializer(128, 256), out bool changed));
            Assert.IsTrue(changed);

            ComputeResourceBinding<ReadWriteBuffer<int>> first = host.GetBinding<ReadWriteBuffer<int>>(0, 0);
            ComputeResourceBinding<ReadWriteBuffer<int>> second = host.GetBinding<ReadWriteBuffer<int>>(0, 1);

            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.AreEqual(128, first.Resource!.Length);
            Assert.AreEqual(256, second.Resource!.Length);
            Assert.AreEqual(first.SetId, second.SetId);
            Assert.AreNotEqual(first.GenerationId, second.GenerationId);
            Assert.AreEqual(0, first.ResourceIndex);
            Assert.AreEqual(1, second.ResourceIndex);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesAReadOnlyResourceForAReadAccessContract(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadOnlyBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            CreateDescriptor(ResourcePlanKind.Buffer, ComputeResourceAccess.Read),
            1,
            [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [64], new BufferMaterializer(64), out _));

            Assert.IsTrue(host.GetBinding<ReadOnlyBuffer<int>>(0, 0).IsValid);
            Assert.IsFalse(host.GetBinding<ReadWriteBuffer<int>>(0, 0).IsValid);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void PublishesATexture2DGeneration(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteTexture2D<float>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Texture2D), 1, [slot]);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [64, 32], new Texture2DMaterializer(64, 32), out _));

            ComputeResourceBinding<ReadWriteTexture2D<float>> binding = host.GetBinding<ReadWriteTexture2D<float>>(0, 0);

            Assert.IsTrue(binding.IsValid);
            Assert.AreEqual(64, binding.Resource!.Width);
            Assert.AreEqual(32, binding.Resource.Height);
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void AccountsTheOwnedBytesOfEveryPublishedGeneration(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);
        ulong expected = GetExpectedBufferBytes(graphicsDevice, 4096);

        Assert.AreNotEqual(0ul, expected);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [4096], new BufferMaterializer(4096), out _));

            Assert.AreEqual(before + expected, GetOwnedBytes(graphicsDevice));

            Assert.IsTrue(host.TryEnsureResource(0, [4096], new BufferMaterializer(4096), out _));

            Assert.AreEqual(before + expected, GetOwnedBytes(graphicsDevice));
        }
        finally
        {
            host.Dispose();
        }

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesTheRetiredGenerationOfEveryReplacement(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        try
        {
            Assert.IsTrue(host.TryEnsureResource(0, [2048], new BufferMaterializer(2048), out _));
            Assert.IsTrue(host.TryEnsureResource(0, [2048 * 2], new BufferMaterializer(2048 * 2), out _));
            Assert.IsTrue(host.TryEnsureResource(0, [2048 * 3], new BufferMaterializer(2048 * 3), out _));

            Assert.AreEqual(before + GetExpectedBufferBytes(graphicsDevice, 2048 * 3), GetOwnedBytes(graphicsDevice));
        }
        finally
        {
            host.Dispose();
        }

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsDeclarationsThatDoNotMatchTheRequestedPlan(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => host.TryEnsureResource(0, [1024], new BufferMaterializer(512), out _));

            Assert.IsFalse(slot.IsAllocated);
            Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsMaterializersThatDoNotDeclareEveryMember(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceGroupSlot<object> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            CreateDescriptor(ResourcePlanKind.ResourceGroup, resourceCount: 2),
            1,
            [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => host.TryEnsureResource(0, [128, 256], new PartialGroupMaterializer(128), out _));

            Assert.IsFalse(slot.IsAllocated);
            Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsMaterializersThatDeclareDifferentlyInEachStage(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => host.TryEnsureResource(0, [1024], new DriftingMaterializer([1024, 2048]), out _));

            Assert.IsFalse(slot.IsAllocated);
            Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

            Assert.IsTrue(host.TryEnsureResource(0, [1024], new BufferMaterializer(1024), out _));
        }
        finally
        {
            host.Dispose();
        }

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RollsBackEveryGenerationOfAThrowingMaterializer(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        try
        {
            _ = Assert.ThrowsExactly<NotSupportedException>(
                () => host.TryEnsureResource(0, [1024], new ThrowingMaterializer(1024), out _));

            Assert.IsFalse(slot.IsAllocated);
            Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

            Assert.IsTrue(host.TryEnsureResource(0, [1024], new BufferMaterializer(1024), out _));
        }
        finally
        {
            host.Dispose();
        }

        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsOwnedSlotsWithoutAnAccessContract(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(
            graphicsDevice,
            CreateDescriptor(ResourcePlanKind.Buffer, hasAccessContracts: false),
            1,
            [slot]);

        try
        {
            _ = Assert.ThrowsExactly<InvalidOperationException>(
                () => host.TryEnsureResource(0, [32], new BufferMaterializer(32), out _));
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void RejectsPlanVectorsThatDoNotMatchTheSlotContract(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        try
        {
            _ = Assert.ThrowsExactly<ArgumentException>(
                () => host.TryEnsureResource(0, [1024, 1], new BufferMaterializer(1024), out _));

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => host.TryEnsureResource(0, [0], new BufferMaterializer(0), out _));

            _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => host.TryEnsureResource(1, [1024], new BufferMaterializer(1024), out _));
        }
        finally
        {
            host.Dispose();
        }
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReleasesEveryGenerationOnDispose(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();
        ComputeResourceSlot<ReadWriteBuffer<int>> slot = new();
        ComputeHostRuntime host = ComputeHostRuntime.Create(graphicsDevice, CreateDescriptor(ResourcePlanKind.Buffer), 1, [slot]);

        ulong before = GetStableOwnedBytes(graphicsDevice);

        Assert.IsTrue(host.TryEnsureResource(0, [1024], new BufferMaterializer(1024), out _));
        Assert.IsFalse(host.IsDisposeRequested);

        host.Dispose();

        Assert.IsTrue(host.IsDisposeRequested);
        Assert.IsTrue(slot.IsDisposeRequested);
        Assert.IsFalse(slot.IsAllocated);
        Assert.IsFalse(host.GetBinding<ReadWriteBuffer<int>>(0, 0).IsValid);
        Assert.AreEqual(before, GetOwnedBytes(graphicsDevice));

        host.Dispose();
        host.WaitForDisposal();
        slot.WaitForDisposal();
    }
}
