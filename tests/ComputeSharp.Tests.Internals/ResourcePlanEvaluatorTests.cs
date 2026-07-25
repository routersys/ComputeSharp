using System;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class ResourcePlanEvaluatorTests
{
    private static ResourcePlanFieldDescriptor Field(uint fieldOrdinal, uint slotResourceIndex, ResourcePlanDimensionKind dimensionKind)
    {
        return new ResourcePlanFieldDescriptor(
            fieldOrdinal,
            slotResourceIndex,
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            "memberLength",
            dimensionKind);
    }

    private static OwnedSlotDescriptor BufferSlot()
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "Member",
            "ComputeSharp.ReadWriteBuffer`1",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Buffer,
            ComputeResourceRecovery.Discardable,
            new[] { Field(0, 0, ResourcePlanDimensionKind.Length) });
    }

    private static OwnedSlotDescriptor Texture2DSlot()
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "Member",
            "ComputeSharp.ReadWriteTexture2D`1",
            ResourceOwnershipKind.OwnedSlot,
            ResourcePlanKind.Texture2D,
            ComputeResourceRecovery.Discardable,
            new[]
            {
                Field(0, 0, ResourcePlanDimensionKind.Width),
                Field(1, 0, ResourcePlanDimensionKind.Height)
            });
    }

    private static OwnedSlotDescriptor GroupSlot()
    {
        return new OwnedSlotDescriptor(
            new SlotOrdinal(0),
            "Member",
            "Ukiyoe.GridResources",
            ResourceOwnershipKind.OwnedGroupSlot,
            ResourcePlanKind.ResourceGroup,
            ComputeResourceRecovery.Discardable,
            new[]
            {
                Field(0, 0, ResourcePlanDimensionKind.Length),
                Field(1, 1, ResourcePlanDimensionKind.Width),
                Field(2, 1, ResourcePlanDimensionKind.Height)
            });
    }

    private static SharedTextureContractDescriptor SharedTexture(ComputeResourceResizePolicy resizePolicy)
    {
        return new SharedTextureContractDescriptor(
            new SlotOrdinal(0),
            "Source",
            "ComputeSharp.Interop.ComputeSharedTexture2D`1",
            resizePolicy,
            ComputeResourceAccess.ReadWrite,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost);
    }

    [TestMethod]
    public void AcceptsPositivePlanScalars()
    {
        ResourcePlanEvaluator.ValidatePlan(BufferSlot(), [1], "plan");
        ResourcePlanEvaluator.ValidatePlan(Texture2DSlot(), [1, 1], "plan");
        ResourcePlanEvaluator.ValidatePlan(GroupSlot(), [16, 8, 4], "plan");
    }

    [TestMethod]
    public void RejectsNonPositivePlanScalars()
    {
        OwnedSlotDescriptor slot = GroupSlot();

        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ResourcePlanEvaluator.ValidatePlan(slot, [0, 8, 4], "plan"));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ResourcePlanEvaluator.ValidatePlan(slot, [16, -1, 4], "plan"));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ResourcePlanEvaluator.ValidatePlan(slot, [16, 8, 0], "plan"));
    }

    [TestMethod]
    public void RejectsPlanWithMismatchedFieldCount()
    {
        OwnedSlotDescriptor slot = Texture2DSlot();

        _ = Assert.ThrowsException<ArgumentException>(() => ResourcePlanEvaluator.ValidatePlan(slot, [1], "plan"));
        _ = Assert.ThrowsException<ArgumentException>(() => ResourcePlanEvaluator.Evaluate(slot, [1, 1, 1], [1, 1]));
        _ = Assert.ThrowsException<ArgumentException>(() => ResourcePlanEvaluator.Evaluate(slot, [1, 1], [1]));
    }

    [TestMethod]
    public void ReportsIdenticalBufferPlan()
    {
        Assert.AreEqual(ResourcePlanDecision.Identical, ResourcePlanEvaluator.Evaluate(BufferSlot(), [1024], [1024]));
    }

    [TestMethod]
    public void ReportsReplacementForDifferentBufferLength()
    {
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(BufferSlot(), [1024], [512]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(BufferSlot(), [512], [1024]));
    }

    [TestMethod]
    public void ReportsReplacementForAnyDifferentTextureDimension()
    {
        OwnedSlotDescriptor slot = Texture2DSlot();

        Assert.AreEqual(ResourcePlanDecision.Identical, ResourcePlanEvaluator.Evaluate(slot, [640, 480], [640, 480]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(slot, [641, 480], [640, 480]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(slot, [640, 481], [640, 480]));
    }

    [TestMethod]
    public void ReplacesWholeGroupWhenAnyMemberDiffers()
    {
        OwnedSlotDescriptor slot = GroupSlot();

        Assert.AreEqual(ResourcePlanDecision.Identical, ResourcePlanEvaluator.Evaluate(slot, [16, 8, 4], [16, 8, 4]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(slot, [17, 8, 4], [16, 8, 4]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(slot, [16, 9, 4], [16, 8, 4]));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.Evaluate(slot, [16, 8, 5], [16, 8, 4]));
    }

    [TestMethod]
    public void NeverReportsLogicalUpdateForOwnedSlot()
    {
        OwnedSlotDescriptor slot = Texture2DSlot();

        Assert.AreNotEqual(ResourcePlanDecision.LogicalUpdate, ResourcePlanEvaluator.Evaluate(slot, [320, 240], [640, 480]));
    }

    [TestMethod]
    public void RejectsNonPositiveSharedTextureDimensions()
    {
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ResourcePlanEvaluator.ValidateSharedTexturePlan(0, 480));
        _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ResourcePlanEvaluator.ValidateSharedTexturePlan(640, -1));

        ResourcePlanEvaluator.ValidateSharedTexturePlan(1, 1);
    }

    [TestMethod]
    public void ReplacesExactSharedTextureOnAnyDimensionChange()
    {
        SharedTextureContractDescriptor sharedTexture = SharedTexture(ComputeResourceResizePolicy.Exact);

        Assert.AreEqual(ResourcePlanDecision.Identical, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 640, 480, 640, 480, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 320, 240, 640, 480, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 800, 480, 640, 480, 640, 480));
    }

    [TestMethod]
    public void ReusesGrowOnlySharedTextureWithinPhysicalCapacity()
    {
        SharedTextureContractDescriptor sharedTexture = SharedTexture(ComputeResourceResizePolicy.GrowOnly);

        Assert.AreEqual(ResourcePlanDecision.LogicalUpdate, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 320, 240, 640, 480, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.LogicalUpdate, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 640, 480, 320, 240, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Identical, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 320, 240, 320, 240, 640, 480));
    }

    [TestMethod]
    public void ReplacesGrowOnlySharedTextureBeyondPhysicalCapacity()
    {
        SharedTextureContractDescriptor sharedTexture = SharedTexture(ComputeResourceResizePolicy.GrowOnly);

        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 641, 480, 640, 480, 640, 480));
        Assert.AreEqual(ResourcePlanDecision.Replacement, ResourcePlanEvaluator.EvaluateSharedTexture(sharedTexture, 640, 481, 640, 480, 640, 480));
    }
}
