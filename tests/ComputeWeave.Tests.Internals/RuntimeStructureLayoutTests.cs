using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class RuntimeStructureLayoutTests
{
    private readonly struct AlignmentProbe<T>(byte head, T value)
    {
        public byte Head { get; } = head;

        public T Value { get; } = value;
    }

    private static readonly (Type Type, int ByteLength)[] TrackedStructures =
    [
        (typeof(ResourceGenerationRecord), 96),
        (typeof(ResourceGenerationBinding), 120),
        (typeof(SlotControlRecord), 72),
        (typeof(SlotResourcePlanStateRecord), 8),
        (typeof(SlotTrimEntry), 48),
        (typeof(OwnedSlotResourceLayout), 12),
        (typeof(PendingSubmissionRecord), 160),
        (typeof(GraphicsResourceUsageEntry), 32),
        (typeof(RecordingBundleEntry), 12),
        (typeof(ResourceGenerationPin), 32),
        (typeof(ResourceGenerationSetHandle), 16),
        (typeof(ResourceUsageBinding), 32),
        (typeof(ResourceBarrierPlanEntry), 8),
        (typeof(FencePoint), 16),
        (typeof(HostRegistrationRecord), 40),
        (typeof(ResourceSetRegistrationRecord), 24)
    ];

    private static int GetByteLength(Type type)
    {
        return (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null)!;
    }

    private static int GetAlignment(Type type)
    {
        return GetByteLength(typeof(AlignmentProbe<>).MakeGenericType(type)) - GetByteLength(type);
    }

    private static int GetFieldByteTotal(Type type)
    {
        int total = 0;

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            total += GetByteLength(field.FieldType);
        }

        return total;
    }

    [TestMethod]
    public void KeepsTheMeasuredByteLengthOfEveryTrackedRuntimeStructure()
    {
        foreach ((Type type, int byteLength) in TrackedStructures)
        {
            Assert.AreEqual(byteLength, GetByteLength(type), type.Name);
        }
    }

    [TestMethod]
    public void KeepsEveryTrackedRuntimeStructureWithinItsAlignmentPadding()
    {
        foreach ((Type type, _) in TrackedStructures)
        {
            int padding = GetByteLength(type) - GetFieldByteTotal(type);
            int alignment = GetAlignment(type);

            Assert.IsTrue(padding >= 0, $"{type.Name} reports {padding} bytes of padding.");
            Assert.IsTrue(padding < alignment, $"{type.Name} wastes {padding} bytes against an alignment of {alignment} bytes.");
        }
    }

    [TestMethod]
    public void TracksEveryRuntimeStructureThatIsHeldPerInstance()
    {
        Assert.AreEqual(16, TrackedStructures.Length);

        foreach ((Type type, _) in TrackedStructures)
        {
            Assert.IsTrue(type.IsValueType, type.Name);
            Assert.IsFalse(type.IsGenericType, type.Name);
        }
    }
}
