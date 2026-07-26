using ComputeSharp.Graphics.Extensions;
using ComputeSharp.Graphics.Pipelines;
using ComputeSharp.Memory;
using ComputeSharp.Resources.Plans;
using ComputeSharp.Tests.Attributes;
using ComputeSharp.Tests.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ResourceType = ComputeSharp.Graphics.Resources.Enums.ResourceType;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public unsafe partial class ComputeGenerationDescriberTests
{
    private static OwnedSlotDescriptor BufferSlot(uint ordinal = 0)
    {
        ResourcePlanFieldDescriptor[] fields =
        [
            new ResourcePlanFieldDescriptor(0, 0, "S", "B", "sLength", ResourcePlanDimensionKind.Length)
        ];

        return new OwnedSlotDescriptor(
            new SlotOrdinal(ordinal),
            "S",
            "B",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Buffer,
            ComputeResourceRecovery.Discardable,
            fields);
    }

    private static OwnedSlotDescriptor Texture2DSlot()
    {
        ResourcePlanFieldDescriptor[] fields =
        [
            new ResourcePlanFieldDescriptor(0, 0, "S", "T", "sWidth", ResourcePlanDimensionKind.Width),
            new ResourcePlanFieldDescriptor(1, 0, "S", "T", "sHeight", ResourcePlanDimensionKind.Height)
        ];

        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "S",
            "T",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Texture2D,
            ComputeResourceRecovery.Discardable,
            fields);
    }

    private static OwnedSlotDescriptor GroupSlot()
    {
        ResourcePlanFieldDescriptor[] fields =
        [
            new ResourcePlanFieldDescriptor(0, 0, "A", "B", "aLength", ResourcePlanDimensionKind.Length),
            new ResourcePlanFieldDescriptor(1, 1, "B", "B", "bLength", ResourcePlanDimensionKind.Length)
        ];

        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "G",
            "G",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.ResourceGroup,
            ComputeResourceRecovery.Discardable,
            fields);
    }

    private static ComputeGenerationDeclaration Declaration(
        ComputeGenerationShape shape,
        int width,
        int height,
        MemoryPlacement placement = MemoryPlacement.Local,
        ulong sizeInBytes = 65536)
    {
        return new ComputeGenerationDeclaration
        {
            Shape = shape,
            Width = width,
            Height = height,
            Placement = placement,
            SizeInBytes = sizeInBytes
        };
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DescribesABufferWithTheAllocationInfoOfItsOwnDescription(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.DescribeBuffer<float>(
                graphicsDevice,
                ComputeResourceAccess.ReadWrite,
                1024,
                out ComputeGenerationDeclaration declaration));

        GraphicsCommittedResourceDescription expected = ID3D12DeviceExtensions.GetCommittedResourceDescription(
            ResourceType.ReadWrite,
            1024ul * sizeof(float),
            graphicsDevice.IsCacheCoherentUMA);

        Assert.AreEqual(ComputeGenerationShape.Buffer, declaration.Shape);
        Assert.AreEqual(1024, declaration.Width);
        Assert.AreEqual(MemoryPlacement.Local, declaration.Placement);
        Assert.AreEqual(
            graphicsDevice.D3D12Device->GetResourceAllocationInfo(in expected).SizeInBytes,
            declaration.SizeInBytes);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void DescribesATexture2DWithTheAllocationInfoOfItsOwnDescription(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.DescribeTexture2D<float>(
                graphicsDevice,
                ComputeResourceAccess.ReadWrite,
                128,
                64,
                out ComputeGenerationDeclaration declaration));

        Assert.AreEqual(ComputeGenerationShape.Texture2D, declaration.Shape);
        Assert.AreEqual(128, declaration.Width);
        Assert.AreEqual(64, declaration.Height);
        Assert.AreEqual(MemoryPlacement.Local, declaration.Placement);
        Assert.AreNotEqual(0ul, declaration.SizeInBytes);
    }

    [CombinatorialTestMethod]
    [AllDevices]
    public void ReportsTheReadOnlyResourceTypeForReadAccess(Device device)
    {
        GraphicsDevice graphicsDevice = device.Get();

        Assert.AreEqual(ResourceType.ReadOnly, ComputeGenerationDescriber.GetResourceType(ComputeResourceAccess.Read));
        Assert.AreEqual(ResourceType.ReadWrite, ComputeGenerationDescriber.GetResourceType(ComputeResourceAccess.ReadWrite));
        Assert.AreEqual(ResourceType.ReadWrite, ComputeGenerationDescriber.GetResourceType(ComputeResourceAccess.Write));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.DescribeBuffer<int>(graphicsDevice, ComputeResourceAccess.Read, 16, out _));
    }

    [TestMethod]
    public void AcceptsDeclarationsThatMatchTheRequestedPlan()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                BufferSlot(),
                [1024],
                [Declaration(ComputeGenerationShape.Buffer, 1024, 1)]));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                Texture2DSlot(),
                [128, 64],
                [Declaration(ComputeGenerationShape.Texture2D, 128, 64)]));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                GroupSlot(),
                [16, 32],
                [Declaration(ComputeGenerationShape.Buffer, 16, 1), Declaration(ComputeGenerationShape.Buffer, 32, 1)]));
    }

    [TestMethod]
    public void RejectsDeclarationsWhoseDimensionsDisagreeWithTheRequestedPlan()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.DimensionMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                BufferSlot(),
                [1024],
                [Declaration(ComputeGenerationShape.Buffer, 1023, 1)]));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.DimensionMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                Texture2DSlot(),
                [128, 64],
                [Declaration(ComputeGenerationShape.Texture2D, 128, 63)]));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.DimensionMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                BufferSlot(),
                [1024, 1],
                [Declaration(ComputeGenerationShape.Buffer, 1024, 1)]));
    }

    [TestMethod]
    public void RejectsDeclarationsWhoseShapeDisagreesWithTheSlotDescriptor()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.ShapeMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                BufferSlot(),
                [1024],
                [Declaration(ComputeGenerationShape.Texture2D, 1024, 1)]));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.ShapeMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                Texture2DSlot(),
                [128, 64],
                [Declaration(ComputeGenerationShape.Buffer, 128, 64)]));
    }

    [TestMethod]
    public void RejectsDeclarationCountsThatDoNotCoverEveryPlanField()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.CountMismatch,
            ComputeGenerationDescriber.ValidateAgainstPlan(
                GroupSlot(),
                [16, 32],
                [Declaration(ComputeGenerationShape.Buffer, 16, 1)]));
    }

    [TestMethod]
    public void SumsEveryDeclarationOfASinglePlacement()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.Valid,
            ComputeGenerationDescriber.ValidatePlacement(
                [
                    Declaration(ComputeGenerationShape.Buffer, 16, 1, MemoryPlacement.Local, 65536),
                    Declaration(ComputeGenerationShape.Buffer, 32, 1, MemoryPlacement.Local, 131072)
                ],
                out MemoryPlacement placement,
                out ulong totalSizeInBytes));

        Assert.AreEqual(MemoryPlacement.Local, placement);
        Assert.AreEqual(196608ul, totalSizeInBytes);
    }

    [TestMethod]
    public void RejectsDeclarationsThatSpanMoreThanOnePlacement()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.PlacementMismatch,
            ComputeGenerationDescriber.ValidatePlacement(
                [
                    Declaration(ComputeGenerationShape.Buffer, 16, 1, MemoryPlacement.Local),
                    Declaration(ComputeGenerationShape.Buffer, 32, 1, MemoryPlacement.NonLocal)
                ],
                out MemoryPlacement placement,
                out ulong totalSizeInBytes));

        Assert.AreEqual(MemoryPlacement.Local, placement);
        Assert.AreEqual(0ul, totalSizeInBytes);
    }

    [TestMethod]
    public void RejectsAnEmptyDeclarationSetAndAnOverflowingSum()
    {
        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.CountMismatch,
            ComputeGenerationDescriber.ValidatePlacement([], out _, out _));

        Assert.AreEqual(
            ComputeGenerationDeclarationStatus.AllocationInfoInvalid,
            ComputeGenerationDescriber.ValidatePlacement(
                [
                    Declaration(ComputeGenerationShape.Buffer, 16, 1, MemoryPlacement.Local, ulong.MaxValue),
                    Declaration(ComputeGenerationShape.Buffer, 32, 1, MemoryPlacement.Local, 1)
                ],
                out _,
                out ulong totalSizeInBytes));

        Assert.AreEqual(0ul, totalSizeInBytes);
    }

    [TestMethod]
    public void ComparesDeclarationsByShapeAndDimensionsOnly()
    {
        ComputeGenerationDeclaration first = Declaration(ComputeGenerationShape.Buffer, 16, 1, MemoryPlacement.Local, 65536);
        ComputeGenerationDeclaration second = Declaration(ComputeGenerationShape.Buffer, 16, 1, MemoryPlacement.NonLocal, 131072);

        Assert.IsTrue(first.IsSameDeclaration(in second));

        Assert.IsFalse(first.IsSameDeclaration(Declaration(ComputeGenerationShape.Buffer, 17, 1)));
        Assert.IsFalse(first.IsSameDeclaration(Declaration(ComputeGenerationShape.Texture2D, 16, 1)));
        Assert.IsFalse(first.IsSameDeclaration(Declaration(ComputeGenerationShape.Buffer, 16, 2)));
    }
}
