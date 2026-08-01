using ComputeWeave.Graphics.Pipelines;
using ComputeWeave.Resources.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeWeave.Tests.Internals;

[TestClass]
public class ResourceContractValidatorTests
{
    private sealed class GenerationOwner(ulong setId) : IResourceGenerationOwner
    {
        private ResourceGenerationRecord record;

        public ResourceGenerationSetId SetId { get; } = new(setId);

        public int ResourceCount => 1;

        public ref ResourceGenerationRecord GetResourceRecord(int resourceOrdinal)
        {
            return ref this.record;
        }

        public unsafe ComputeWeave.Win32.ID3D12Resource* GetResourceNativePointer(int resourceOrdinal)
        {
            return null;
        }
    }

    private static PipelineDescriptor Pipeline(
        ResourceContractDescriptor[] parameters,
        ResourceContractDescriptor[]? internalResources = null)
    {
        return new PipelineDescriptor(
            new PipelineOrdinal(0),
            "M",
            "S",
            PipelineFlags.None,
            checked(parameters.Length + (internalResources?.Length ?? 0)),
            1,
            parameters,
            internalResources ?? []);
    }

    private static ResourceContractDescriptor Contract(
        uint ordinal,
        ComputeResourceAccess access,
        ComputeResourceAliasing aliasing = ComputeResourceAliasing.Disallow)
    {
        return new ResourceContractDescriptor(
            new ResourceOrdinal(ordinal),
            "ComputeWeave.ReadWriteBuffer`1[System.Int32]",
            access,
            ComputeResourceSharing.Internal,
            aliasing,
            ResourceOwnershipKind.Borrowed,
            false,
            new SlotOrdinal(0),
            0);
    }

    private static GraphicsResourceUsageEntry Usage(ulong generation, ComputeResourceAccess access)
    {
        return new GraphicsResourceUsageEntry
        {
            Set = new ResourceGenerationSetHandle(new GenerationOwner(generation)),
            ResourceIndex = 0,
            Generation = new ResourceGenerationId(generation),
            Access = access,
            FirstState = TrackedResourceState.Common,
            FinalState = TrackedResourceState.Common
        };
    }

    [TestMethod]
    public void AcceptsAnObservedAccessWithinEveryDeclaredContract()
    {
        PipelineDescriptor pipeline = Pipeline(
            [Contract(0, ComputeResourceAccess.ReadWrite)],
            [Contract(1, ComputeResourceAccess.Read)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.Valid,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7), new ResourceGenerationId(9)],
                [Usage(7, ComputeResourceAccess.Write), Usage(9, ComputeResourceAccess.Read)]));
    }

    [TestMethod]
    public void RejectsAnObservedAccessBeyondTheDeclaredContract()
    {
        PipelineDescriptor pipeline = Pipeline([Contract(0, ComputeResourceAccess.Read)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.ObservedAccessExceedsDeclared,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.ReadWrite)]));

        Assert.AreEqual(
            ResourceContractValidationStatus.ObservedAccessExceedsDeclared,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.Write)]));
    }

    [TestMethod]
    public void RequiresEveryCollidingContractToAllowAliasing()
    {
        PipelineDescriptor allowed = Pipeline(
        [
            Contract(0, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Allow),
            Contract(1, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Allow)
        ]);

        Assert.AreEqual(
            ResourceContractValidationStatus.Valid,
            ResourceContractValidator.Validate(
                in allowed,
                [new ResourceGenerationId(7), new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.ReadWrite)]));

        PipelineDescriptor mixed = Pipeline(
        [
            Contract(0, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Allow),
            Contract(1, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Disallow)
        ]);

        Assert.AreEqual(
            ResourceContractValidationStatus.AliasingNotAllowed,
            ResourceContractValidator.Validate(
                in mixed,
                [new ResourceGenerationId(7), new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.ReadWrite)]));
    }

    [TestMethod]
    public void RejectsAliasingWhenAnyOfThreeContractsDisallowsIt()
    {
        PipelineDescriptor pipeline = Pipeline(
        [
            Contract(0, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Allow),
            Contract(1, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Disallow),
            Contract(2, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Allow)
        ]);

        Assert.AreEqual(
            ResourceContractValidationStatus.AliasingNotAllowed,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7), new ResourceGenerationId(7), new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.ReadWrite)]));
    }

    [TestMethod]
    public void AcceptsASingleContractThatDisallowsAliasing()
    {
        PipelineDescriptor pipeline = Pipeline([Contract(0, ComputeResourceAccess.ReadWrite, ComputeResourceAliasing.Disallow)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.Valid,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7)],
                [Usage(7, ComputeResourceAccess.ReadWrite)]));
    }

    [TestMethod]
    public void RejectsAnObservedGenerationWithoutAnyContract()
    {
        PipelineDescriptor pipeline = Pipeline([Contract(0, ComputeResourceAccess.ReadWrite)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.UndeclaredGeneration,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7)],
                [Usage(9, ComputeResourceAccess.Read)]));
    }

    [TestMethod]
    public void RejectsAnUnboundContractAndAMismatchedContractCount()
    {
        PipelineDescriptor pipeline = Pipeline([Contract(0, ComputeResourceAccess.ReadWrite)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.UnboundGeneration,
            ResourceContractValidator.Validate(in pipeline, [default], []));

        Assert.AreEqual(
            ResourceContractValidationStatus.ContractCountMismatch,
            ResourceContractValidator.Validate(in pipeline, [], []));

        Assert.AreEqual(
            ResourceContractValidationStatus.ContractCountMismatch,
            ResourceContractValidator.Validate(
                in pipeline,
                [new ResourceGenerationId(7), new ResourceGenerationId(9)],
                []));
    }

    [TestMethod]
    public void AcceptsAPipelineWithoutAnyObservedResource()
    {
        PipelineDescriptor pipeline = Pipeline([Contract(0, ComputeResourceAccess.ReadWrite)]);

        Assert.AreEqual(
            ResourceContractValidationStatus.Valid,
            ResourceContractValidator.Validate(in pipeline, [new ResourceGenerationId(7)], []));
    }
}
