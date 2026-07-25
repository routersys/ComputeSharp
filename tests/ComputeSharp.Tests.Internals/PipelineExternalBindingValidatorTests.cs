using System.IO;
using ComputeSharp.Graphics.Pipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.Internals;

[TestClass]
public class PipelineExternalBindingValidatorTests
{
    private const string TextureTypeMetadataName = "ComputeSharp.ReadWriteTexture2D`2[ComputeSharp.Bgra32,ComputeSharp.Float4]";

    private static ResourceContractDescriptor Parameter(
        string resourceTypeMetadataName = TextureTypeMetadataName,
        ComputeResourceAccess access = ComputeResourceAccess.ReadWrite,
        ComputeResourceSharing sharing = ComputeResourceSharing.External,
        ResourceOwnershipKind ownership = ResourceOwnershipKind.Borrowed,
        bool hasSlot = false,
        uint slot = 0,
        uint slotResourceIndex = 0)
    {
        return new ResourceContractDescriptor(
            new ResourceOrdinal(0),
            resourceTypeMetadataName,
            access,
            sharing,
            ComputeResourceAliasing.Disallow,
            ownership,
            hasSlot,
            new SlotOrdinal(slot),
            slotResourceIndex);
    }

    private static SharedTextureContractDescriptor SharedTexture(
        string resourceTypeMetadataName = TextureTypeMetadataName,
        ComputeResourceAccess computeAccess = ComputeResourceAccess.ReadWrite,
        uint ordinal = 0,
        string memberMetadataName = "Source")
    {
        return new SharedTextureContractDescriptor(
            new SlotOrdinal(ordinal),
            memberMetadataName,
            resourceTypeMetadataName,
            ComputeResourceResizePolicy.Exact,
            computeAccess,
            ExternalResourceAccess.Write,
            ExternalTextureUsage.RenderTarget,
            ComputeAlphaMode.Premultiplied,
            ComputeSharedTextureInitialOwner.External,
            ComputeResourceRecovery.RecreateFromHost);
    }

    [TestMethod]
    public void AcceptsMatchingExternalParameter()
    {
        PipelineExternalBindingValidator.Validate(Parameter(), SharedTexture());
    }

    [TestMethod]
    public void RejectsInternalSharing()
    {
        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(sharing: ComputeResourceSharing.Internal), SharedTexture()));
    }

    [TestMethod]
    public void RejectsOwnershipOtherThanBorrowed()
    {
        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(ownership: ResourceOwnershipKind.SharedTextureSlot), SharedTexture()));

        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(ownership: ResourceOwnershipKind.OwnedSlot), SharedTexture()));
    }

    [TestMethod]
    public void RejectsParameterBoundToSlot()
    {
        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(hasSlot: true), SharedTexture()));

        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(slot: 1), SharedTexture()));

        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(slotResourceIndex: 1), SharedTexture()));
    }

    [TestMethod]
    public void RejectsMismatchedResourceType()
    {
        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(resourceTypeMetadataName: "ComputeSharp.ReadWriteBuffer`1[System.Int32]"), SharedTexture()));
    }

    [TestMethod]
    public void RejectsMismatchedComputeAccess()
    {
        _ = Assert.ThrowsException<InvalidDataException>(
            () => PipelineExternalBindingValidator.Validate(Parameter(access: ComputeResourceAccess.Read), SharedTexture()));
    }

    [TestMethod]
    public void AcceptsSameResourceTypeForDistinctSharedTextures()
    {
        SharedTextureContractDescriptor source = SharedTexture(ordinal: 0, memberMetadataName: "Source");
        SharedTextureContractDescriptor output = SharedTexture(ordinal: 1, memberMetadataName: "Output");

        Assert.AreEqual(source.ResourceTypeMetadataName, output.ResourceTypeMetadataName);
        Assert.AreNotEqual(source.Ordinal, output.Ordinal);
        Assert.AreNotEqual(source.MemberMetadataName, output.MemberMetadataName);

        PipelineExternalBindingValidator.Validate(Parameter(), source);
        PipelineExternalBindingValidator.Validate(Parameter(), output);
    }
}
