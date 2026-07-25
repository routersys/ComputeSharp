using ComputeSharp.SourceGenerators;
using ComputeSharp.Tests.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputeSharp.Tests.SourceGenerators.Canonicalization;

[TestClass]
public class ResourcePlanEmissionTests
{
    private const string GroupSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        [ComputeResourceGroup]
        internal sealed partial class GridResources
        {
            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            internal ReadWriteBuffer<float> ColorIn { get; }

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
            internal ReadWriteTexture2D<float> Mask { get; }
        }
        """;

    private const string HostSource = """
        using ComputeSharp;

        namespace Ukiyoe;

        [ComputePipelineHost("device", 1)]
        public sealed partial class Host
        {
            private readonly GraphicsDevice device = null!;

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteBuffer<int>> index = new();

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceSlot<ReadWriteTexture2D<float>> silhouette = new();

            [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
            private readonly ComputeResourceGroupSlot<GridResources> grid = new();

            [ComputePipeline]
            private void Run(in ComputeContext context)
            {
            }
        }
        """;

    private static string RunAndGetSource(string[] sources, string assemblyName, string hintNameFragment)
    {
        CSharpCompilation compilation = CompilationHelper.CreateCompilation(sources, assemblyName);
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        return GeneratorHelper.GetGeneratedSource(GeneratorHelper.Run(driver, compilation, out _), hintNameFragment);
    }

    [TestMethod]
    public void EmitsBufferPlanForOwnedSlot()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "PlanBufferTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("public readonly struct IndexPlan"), source);
        Assert.IsTrue(source.Contains("public IndexPlan(int indexLength)"), source);
        Assert.IsTrue(source.Contains("public int IndexLength { get; }"), source);
    }

    [TestMethod]
    public void EmitsTexturePlanDimensionsInCanonicalOrder()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "PlanTextureTests", "Ukiyoe.Host");

        Assert.IsTrue(source.Contains("public SilhouettePlan(int silhouetteWidth, int silhouetteHeight)"), source);
        Assert.IsTrue(
            source.IndexOf("public int SilhouetteWidth { get; }") < source.IndexOf("public int SilhouetteHeight { get; }"),
            source);
    }

    [TestMethod]
    public void EmitsNoHostPlanForOwnedGroupSlot()
    {
        string source = RunAndGetSource([GroupSource, HostSource], "PlanGroupSlotTests", "Ukiyoe.Host");

        Assert.IsFalse(source.Contains("GridPlan"), source);
    }

    [TestMethod]
    public void EmitsGroupPlanInCanonicalMemberOrder()
    {
        string source = RunAndGetSource([GroupSource], "PlanGroupTests", "Ukiyoe.GridResources");

        Assert.IsTrue(source.Contains("public readonly struct Plan"), source);
        Assert.IsTrue(source.Contains("public Plan(int colorInLength, int maskWidth, int maskHeight)"), source);
        Assert.IsTrue(source.Contains("public int ColorInLength { get; }"), source);
        Assert.IsTrue(source.Contains("public int MaskWidth { get; }"), source);
        Assert.IsTrue(source.Contains("public int MaskHeight { get; }"), source);
    }

    [TestMethod]
    public void EmitsNothingForResourceGroupWithoutMembers()
    {
        const string EmptyGroupSource = """
            using ComputeSharp;

            namespace Ukiyoe;

            [ComputeResourceGroup]
            internal sealed partial class EmptyResources
            {
            }
            """;

        CSharpCompilation compilation = CompilationHelper.CreateCompilation([EmptyGroupSource], "PlanEmptyGroupTests");
        GeneratorDriver driver = GeneratorHelper.CreateDriver(new PipelineDescriptorGenerator());

        Assert.AreEqual(0, GeneratorHelper.Run(driver, compilation, out _).Length);
    }
}
